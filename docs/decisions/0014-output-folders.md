# 0014: EXE の横のフォルダを Assets と Data だけにする

## 背景
ビルドの出力 (EXE のフォルダ)に、`Assets`・`Data` のほか、`Features`・`Shell`・`MmmSdk.WinUI`・`runtimes`・`MmmTool.exe.WebView2`・`Logs` が並んでいた。機能を足すたびに EXE の横のフォルダが増えていく形で、どれが必要なものか見分けにくかった。ユーザーの方針として、アプリのデータは EXE のフォルダの外 (`%LOCALAPPDATA%` など)には置かない (気づかないところで膨らむため)。

## 決定
EXE の横のフォルダは `Assets`(配布物)と `Data`(アプリが書くもの)の 2 つだけにする。
- `runtimes`: アプリの csproj で `RuntimeIdentifier` を `win-x64` にし、ビルドも x64 専用にする。ネイティブ DLL (`WebView2Loader.dll` など)は、全 CPU 分の `runtimes\` ではなく、EXE の横に x64 の分だけ置かれる。出力先のパスには RID を付けない (`Directory.Build.props` の `AppendRuntimeIdentifierToOutputPath=false`。SDK も同じ。VS の「発行」が参照先にも RID を渡したときに、参照先の出力先が変わらないようにするため。`bin\<構成>\<TFM>\` のまま。プラットフォームの段も `AppendPlatformToOutputPath=false` で付けない)
- `Features`・`Shell`・`MmmSdk.WinUI`(画面の XAML を変換した `.xbf` と、SDK の `.xaml`): 中身は `MmmTool.pri` に入っていて、実行時はそちらから読む。ビルドの出力に残る、ばらのファイルは、csproj の `RemoveLooseXamlAfterBuild` で、ビルドのあとに消す
- `MmmTool.exe.WebView2`(WebView2 のキャッシュ): `Data\WebView2` に置く。SDK の `TerminalControl.UserDataFolder` に、アプリの `AppInfo.WebView2Directory` を渡す
- `Logs`(エラーのログ): `Data\Logs` に置く (`AppInfo.LogDirectory`)
- CLI補助の添付の一時保存 (`%TEMP%\MmmTool\...`)は、そのまま (ユーザーの決定。一時ファイルで、終了時に自分で消すため)

## 理由
- `.xbf` は、WinUI のビルドが `.pri` の中に入れたうえで、ばらのファイルも出力へコピーしている (`CopyGeneratedXaml`)。発行 (publish)の出力には、もともとばらのファイルが出ない。ビルドの出力からも消して、発行物と同じ形にそろえる (Debug の実行で、配布と同じ読み込み方を確かめられる)
- ばらのファイルを消すのは、「中身が `.xbf`・`.xaml` だけのフォルダ」と EXE の横の `.xbf` に限る (`Assets`・`Data` のように、ほかのファイルがあるフォルダには触れない)。フォルダ名を決め打ちにしないので、画面のフォルダが増えても直さなくてよい
- WebView2 のキャッシュを `Assets` に置かない理由: `Assets` は配布物 (読み取り専用)の置き場で、実行時に書くキャッシュと混ぜると、配布や上書きのときに区別がつかなくなる
- `Data` の下にサブフォルダで置く理由: EXE の横のフォルダを増やさない。手で直す JSON (`Data` の直下)とは、フォルダで分かれる。アプリのフォルダごと消せば全部消える
- `.pri` に入れる仕組み (Windows SDK のビルドツールの埋め込み)は、もともと有効だった。新しい設定は足していない

## その後
パッケージの DLL を置く `Lib` フォルダを足した ([0015](0015-dll-reduction-and-lib.md))。EXE の横のフォルダは `Assets`・`Data`・`Lib` の 3 つ。

## 最終形にどう近づくか
機能・画面を足しても、EXE の横は `Assets` と `Data` の 2 つのまま変わらない。アプリが書くものはすべて `Data` の下に集まる。
