# 0015: 使わない DLL を入れず、パッケージの DLL を Lib フォルダに置く

## 背景
EXE の横に DLL が 77 個並んでいた。そのうち約 12 個は、使っていない Windows App SDK の部品 (AI・ML・検索・ウィジェット)のもの。Windows App SDK を全部入りのパッケージ (`Microsoft.WindowsAppSDK`)で参照していたため、一緒に入っていた。残りも、EXE の横に並ぶと、どれが自分のアプリのファイルか見分けにくい。

## 決定
- 使わない部品を入れない
  - SDK (MmmSdk.WinUI): 全部入りのパッケージをやめ、使う部品のパッケージだけを参照する (`Microsoft.WindowsAppSDK.WinUI`・`.Foundation`・`.InteractiveExperiences`)
  - アプリ: 全部入りのパッケージを参照したまま、使わない部品 (`Microsoft.WindowsAppSDK.AI`・`.ML`・`.Search`・`.Widgets`)と、それらだけが使う依存 (`Microsoft.Windows.AI.MachineLearning`・`System.Numerics.Tensors`)を `ExcludeAssets="all"` で参照し、中身を出力に入れない
  - DLL は 77 個 → 65 個
- `MmmTool.dll` 以外の DLL を `Lib` フォルダに置く (63 個)
  - パッケージのファイル (`RuntimeCopyLocalItems`・`NativeCopyLocalItems`): csproj の `MovePackageFilesToLib` で、コピー先を `Lib\` に変える
  - パッケージ以外の参照 (自分のプロジェクトの `MmmTool.Core`・`MmmSdk.Core`・`MmmSdk.WinUI` と、その `.pri`・`.xml`、`Microsoft.Web.WebView2.Core.Projection`): `MoveReferencesToLib` で、ビルドのコピー (`_CopyFilesMarkedCopyLocal`)の前に `ReferenceCopyLocalPaths` のコピー先を変える
  - Windows SDK・WinRT (`Microsoft.Windows.SDK.NET`・`WinRT.Runtime`。ランタイムパックの分): 発行では別の経路 (`RuntimePackAsset`)でコピーされるので、`MoveRuntimePackFilesToLib` でそちらのコピー先も変える
  - 起動時に .NET が `Lib` の DLL を見つけられるよう、`MmmTool.deps.json` の各ファイルに `localPath`(実際の置き場所)を書き足す (csproj のインラインタスク `AddLocalPathToDepsFile`。ビルドは出力へのコピーのあと、発行は発行のあと)
  - EXE の横に残るのは、`MmmTool.exe`・`MmmTool.dll`・`MmmTool.deps.json`・`MmmTool.runtimeconfig.json`・`MmmTool.pri` と `Microsoft.Web.WebView2.Core.dll`。前の 5 つは .NET・WinUI が EXE の横から読む。`Microsoft.Web.WebView2.Core.dll` は deps.json に載らないネイティブの部品で、WinUI の WebView2 が EXE のフォルダから読むため、残す
- `.xml`(XML ドキュメントコメントのファイル)は、ビルドの出力にコピーしない (実行には使わないため。検査のために obj には作る)。`Directory.Build.props` の `CopyDocumentationFileToOutputDirectory=false` と、参照先の付随ファイル (`AllowedReferenceRelatedFileExtensions`)から `.xml` を外す。発行の出力には、もともと入れていない
- `MmmTool.pri` は EXE の横に残す (ユーザーの決定。WinUI が EXE の横から画面とリソースを読むため)。`Lib` の `MmmSdk.WinUI.pri` もそのまま
- Release では `.pdb`(デバッグ情報)を出力に入れない (ユーザーの決定。Debug では入れる)。`Directory.Build.props` で、Release の `DebugType` を `none` にし、参照先 (SDK)の `.pdb` もコピーしない (`AllowedReferenceRelatedFileExtensions` から `.pdb` を外す)

## 理由
- アプリが全部入りのパッケージをやめられない理由: CommunityToolkit (`CommunityToolkit.WinUI.Controls.Segmented`)が古い全部入りのパッケージ (1.6)に依存している。アプリが参照をやめると 1.6 が入ってきて、ビルドが通らない。全部入り (2.5.1)を参照して版を決め、使わない部品だけを外す
- SDK は CommunityToolkit の WinUI 部品を使わないので、部品だけの参照にできる
- InteractiveExperiences を明示する理由: WinUI 2.3.9 が求める 2.1.8 は公開されておらず、NuGet が警告 (NU1603)を出して 2.1.9 を選ぶ。全部入りのパッケージ 2.5.1 と同じ 2.1.9 を明示する
- `localPath` は .NET のホスト (hostpolicy)が読む deps.json の項目で、ファイルの実際の置き場所を表す。.NET SDK には、これを書く設定が無い (コピー先を変えても、deps.json には書かれない)。そのため、ビルドの手順の中で書き足す。小さなコンソールアプリで、`localPath` があれば `Lib` から読まれ、無ければ起動時に見つからないことを確かめた
- deps.json への書き足しは、JSON を読み書きせず文字列で行う (VS の MSBuild (.NET Framework)でも動くインラインタスクにするため)。deps.json は .NET SDK が決まった形で書くので、ファイル名をキーにして足せる。すでに足してあれば何もしない (ビルドを繰り返しても二重にならない)
- 公式の設定ではないので、.NET SDK の更新で deps.json の形が変わると、効かなくなるおそれがある。そのときは、起動時に DLL が見つからずに落ちるので、気づける。`MovePackageFilesToLib`・`MoveRuntimePackFilesToLib`・`MoveReferencesToLib`・`AddLocalPathToBuildDepsFile`・`AddLocalPathToPublishDepsFile` を消せば、元の形 (EXE の横に並ぶ)に戻る
- Release を起動し、WinRT・WinUI・Windows SDK・Windows App Runtime の起動用の DLL が `Lib` から読まれることを確かめた

## 最終形にどう近づくか
EXE の横は、`MmmTool.*` の 5 ファイルと `Microsoft.Web.WebView2.Core.dll`・`Assets`・`Data`・`Lib` だけになり、パッケージを足しても EXE の横は増えない。使わない Windows App SDK の部品は入らない。
