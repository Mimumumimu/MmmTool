# 全体構成

## プロジェクト
| プロジェクト | 対象 | 役割 |
| --- | --- | --- |
| `MmmTool` | `net10.0-windows10.0.19041.0`（WinUI 3） | アプリ本体（画面・UI サービス・Win32 呼び出し） |
| `MmmTool.Core` | `net10.0` | UI に依存しない処理（Entity・Repository・サービス）。Windows / WinUI を参照しない |
| `external/MmmSdk/MmmSdk.Core` | `net10.0` | 共有部品のうち UI に依存しないもの（JSON の保存・設定ストア・位置保存・パスを開く・通知の項目・毎分のスケジューラー・添付の一時保存・多重起動の防止・エラーログ・`Forget`） |
| `external/MmmSdk/MmmSdk.WinUI` | `net10.0-windows10.0.19041.0` | 共有部品のうち UI・Windows に依存するもの（通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択・擬似モーダル・タスクトレイ・ConPTY・コントロール・IME・ウィンドウ補助・エラー処理。Win32 の宣言は CsWin32 でここに集める） |

参照の向きは `MmmTool → MmmTool.Core → MmmSdk.Core`、`MmmTool → MmmSdk.WinUI → MmmSdk.Core`。SDK はアプリを知らない。SDK は別リポジトリ（`https://github.com/Mimumumimu/MmmSdk`）で、Git サブモジュール `external/MmmSdk` として取り込む（clone は `--recurse-submodules`、取りこぼしたら `git submodule update --init --recursive`）。SDK を直したら、サブモジュールの中（master）でコミット・push してから、アプリ側で「新しいコミットを指す」コミットをする（SDK が先）。

何をどこに置くか：UI・Windows に依存しない処理はアプリの Core、どのアプリでも使える汎用の部品は SDK、アプリ固有の画面・機能はアプリ本体。

## フォルダ（機能別）
プロジェクトの中は機能ごとのフォルダ（Vertical Slice）。層ごとのフォルダ（`Views/` `ViewModels/` 等）は作らない。名前空間はフォルダどおり。MVVM の役割はクラス名で分かるようにする（View = `*Page` / `*Window`、ViewModel = `*ViewModel`、Model = Core の Entity・サービス）。決めた理由は [decisions/0001-feature-folders.md](decisions/0001-feature-folders.md)。

```
MmmTool.Core/<機能>/            Entity・Repository のインターフェース・サービス（CliAssist / Links / Reminders）
MmmTool.Core/<機能>/Json/       JSON の実装（Json<名前>Repository）と機能ごとのソース生成 Context
MmmTool/Features/<機能>/        機能全体のつなぎ（Add<機能>()・<機能>Startup・<機能>TrayMenuSource など、Shell へ登録するもの）と、
                                複数の画面で共有するもの（ViewModel の基底クラス・行の書式・I<機能>DialogService）だけ
                                （CliAssist / Links / Reminders / Settings / Debugging）
MmmTool/Features/<機能>/Main/   その機能の入口の画面（サイドバーのページ、またはトレイから開くメインのウィンドウ）の
                                View・ViewModel・行の型。画面が 1 つだけの機能も Main/ に入れる
MmmTool/Features/<機能>/<画面>/ そのほかの画面（Reminders/{Input, List, Settings}、CliAssist/WorkingDirectory）。
                                1 画面 = 1 フォルダ。その画面の View・ViewModel・行の型・その画面だけのサービスを一緒に置く
MmmTool/Shell/                  画面の枠。直下に DI 登録と、機能が登録に使う型・アプリ全体で使う型
                                （NavigationItem / NavigationPage / NavigationArea・SettingsSection・IStartupTask・
                                AppIcon・AppInfo・ShellServiceCollectionExtensions）
MmmTool/Shell/Main/             メインウィンドウ（MainWindow・MainViewModel・PageProvider）
```

- 共有ライブラリ（`external/MmmSdk`）のフォルダは、アプリとは別に、プロジェクトの直下を `Components/<部品>/`・`Controls/`・`Utilities/` の 3 層に分けている（[decisions/0012-sdk-layer-folders.md](decisions/0012-sdk-layer-folders.md)）。アプリの `Features/<機能>/` には同じ層を作らない
- フォルダの決まり（機能の直下 = つなぎ、`Main/` = 入口の画面、`<画面>/` = 1 画面 1 フォルダ）の理由は [decisions/0013-screen-folders.md](decisions/0013-screen-folders.md)。Core には同じ形を当てない（画面が無い）
- `Shell/` は特定の機能を参照しない（機能から共通部分への一方向）。機能固有の画面を開く口は、その機能に置く
- DEBUG 用の機能のフォルダ名は `Debugging`（`Debug` にすると `System.Diagnostics.Debug` を隠すため）

## DI と起動
- Generic Host（`Host.CreateApplicationBuilder`。`DisableDefaults = true` で、使わない設定（appsettings.json・環境変数）とロガーの既定は無効）で DI を組む。ログは SDK の `ErrorLog`（エラーのファイル）だけで、`ILogger` は使わない。`App.ConfigureServices` は「SDK → `AddShell()` → 各機能の `Add<機能>()`」を呼ぶだけ
- 機能を登録した順（CLI補助 → リマインダー → リンク → DEBUG → 設定）に、サイドバーの項目（上部・下部それぞれ）・トレイメニューの項目（リマインダーがリンクより上）・起動時の準備が並ぶ
- 開くたびに作るウィンドウ（`IDisposable` の ViewModel を持つもの）は、`IServiceScopeFactory` で作ったスコープから解決し、閉じたらスコープを破棄する（ルートのプロバイダーから解決した `IDisposable` の Transient は、Host の破棄まで保持され続けるため）。常駐するもの（`PseudoConsoleSession` など）は、Host の破棄で `Dispose` されることを前提に、ルートから解決する
- 保存先（Repository の実装）は各 `Add<機能>()` の「保存先」の行。CLI補助・リンクはローカル専用。DB に替えるなら、リマインダーなど該当機能の行を差し替える
- 設定ページ: 各機能が `AddSettingsSection<TControl>()` で設定の部品を登録し、設定ページは登録順に並べるだけ（[specs/settings.md](specs/settings.md)）
- サイドバー: 各機能が `AddNavigationPage<TPage>(表示名, グリフ, 上部/下部)` で登録する（ページは Transient・キーは型名）。`MainViewModel` が登録から項目を作り、`MainWindow` は `PageProvider`（初回に DI から作ってキャッシュ）からページを受け取る。DEBUG は `AddDebugging()` の中の `#if DEBUG` で、リリースでは登録しない
- 起動時の準備（`IStartupTask`）: `App.OnLaunched` で、`TrayIcon` を解決したあと・`MainWindow` を作る前に、UI スレッドで登録順に `StartAsync` を待つ（共通の設定ファイルの先読み → CLI補助の利用状態の読み込み → リマインダー監視の開始 → リンクの先読み）。決めた理由は [decisions/0002-startup-task.md](decisions/0002-startup-task.md)
- ダイアログ: 共通の `IDialogService`（SDK）は確認ダイアログだけ。機能固有の画面は各機能の口から開く（`IReminderDialogService.ShowInputAsync` / `ShowListAsync`、`IWorkingDirectoryDialogService.ShowAsync`）。実装は SDK の `IDialogHost` の `Owner`（親の決定）と `ShowModalAsync`（開いている間モーダルとして覚える）を使う（具象の `DialogService` には依存しない）。ピッカーの親も `IDialogHost.Owner`
- 終了の順序: `App.ExitAsync` で `MainWindow.PrepareExit`（閉じる要求を素通しにする）→ Host 停止・破棄 → `Exit()`。DI は作った順の逆に破棄するので、`TrayIcon` を画面・各機能（起動時の準備を含む）より先に解決しておき、各機能の後始末のあとにトレイアイコンが消えるようにしている

## エラーの扱い
3 段階。予測できる失敗は、先に確かめる（`TryXxx`・検証・ガード節）。それでも起きる失敗（ファイル・JSON・OS・COM）は、範囲を絞った `catch` で受けて画面に出す（InfoBar）。復旧が難しい失敗・予想外の失敗（バグ）は、ログ（`AppContext.BaseDirectory/Data/Logs/yyyy-MM-dd.log`）→ ダイアログ → 終了（SDK の `FatalErrorHandler`）。隠さず、握りつぶさない。
- `App` のコンストラクターで `FatalErrorHandler` を作って `AttachTo(this)` し（Host を作る前の失敗も拾うため）、DI にも登録する（トレイが使う）
- `try/catch` を書けない場所（`async void`・タイマー・待たれないタスク。例: SDK の `MinuteScheduler` の毎分の処理）の例外は、`AttachTo` の安全網が、同じ処理（ログ → ダイアログ → 終了）で受ける
- 待たずに走らせるタスクは `_ = SomeAsync();` で捨てず、`SomeAsync().Forget()`（SDK の `MmmSdk.Core.Utilities`）にする。捨てると、失敗が誰にも見えず、ガベージコレクションのときに初めて分かる（いつ落ちるか読めない）。`Forget` は、失敗した時点で未処理例外にして、安全網が受ける。起きると分かっている失敗（`DataFileException` など）は、タスクの中で受けて画面に出す（例: `SettingsViewModel.SaveSnoozeIntervalAsync`、`ReminderViewModelBase.RunAsync`）
- `OnLaunched` は、起動が途中で止まって気づけなくならないよう、`try/catch` で包んで報告する。読み込みの失敗など、起動を止めたくない例外は、各機能の `IStartupTask` の中で受ける
- トレイの `WndProc` は、`[UnmanagedCallersOnly]` から例外が抜けるとログも残らず落ちるため、中で `try/catch` して報告する。異常終了のときは、トレイのアイコンも外す
- 詳細は `external/MmmSdk/README.md`「エラーのログと、復旧できないエラーの処理」

## 保存
JSON。場所は `AppContext.BaseDirectory/Data/*.json`。手で修正するときはアプリを閉じてから行う。保存先は将来 SQL Server / DynamoDB などに替える可能性があり、Repository + DI で差し替えられる形にしている。詳細は [specs/storage.md](specs/storage.md)。

## ビルドの共通設定
- リポジトリ直下に `Directory.Build.props`（バージョン・Nullable・ImplicitUsings・`GenerateDocumentationFile`・`EnforceCodeStyleInBuild`・全プロジェクトを x64 専用にする（`Platforms` と既定の `Platform`。Core も含めて AnyCPU を使わない。`-p:Platform` を付けずにビルド・発行しても x64 になる。SDK も同じ設定）・XML ファイルをビルドの出力・発行物に含めない・Release では .pdb を作らない）、`Directory.Packages.props`（中央パッケージ管理。csproj の `PackageReference` にはバージョンを書かない）、`.editorconfig`（`root = true`）を置く。3 つとも slnx の「Solution Items」に入れている
- `.editorconfig`: 未使用 using・ファイル単位の名前空間・using の位置・複数行の本体の波かっこを warning にしてビルドで検査する。1 行の早期 return（`if (x) return;`）は波かっこを省略してよい。`charset` は書かない（BOM 付きの ps1 があるため）
- SDK はリポジトリ直下に自分の同じ 1 組を持つ。MSBuild・.editorconfig は近いほうを使うので、アプリと SDK の設定は混ざらない。共通のパッケージ（CommunityToolkit.Mvvm・Windows App SDK（SDK は部品のパッケージ、アプリは全部入り）・SDK.BuildTools）は SDK を先に上げて、アプリを同じバージョンにする
- バージョンは `Version`（現在 0.1.0。ファイル・アセンブリのバージョンは自動で 0.1.0.0）。製品バージョンの後ろにはコミット番号が付く（.NET の標準の動作）
- XML コメントの検査と未使用 using の検査は、普通の `dotnet build` / VS のビルドでかかる

## 配布
- フレームワーク依存（`SelfContained=false` / `WindowsAppSDKSelfContained=false`）。実行する PC に .NET 10 Desktop Runtime と Windows App Runtime 2.5 が必要。決めた理由は [decisions/0003-framework-dependent.md](decisions/0003-framework-dependent.md)
- 発行は VS の「発行」（プロファイル `win-x64`）、またはコマンドの `dotnet publish .\MmmTool\MmmTool.csproj -p:PublishProfile=win-x64`。どちらも、配布物は `MmmTool/bin/Release/publish/MmmTool_<版>/` にでき、アプリ本体はその中の `MmmTool/`（版は `Directory.Build.props` の `Version`。通常のビルドの出力 `bin/Release/net10.0-windows10.0.19041.0/` の隣。対象の .NET・RID は 1 つずつなので、出力先の名前に入れない）。
  - 2 段階にしている: 発行（`PublishDir`）は、版を含まない途中のフォルダ `obj/Release/publish/` に出し、発行のあとに csproj の `PackageDistribution` が、アプリ本体を `MmmTool_<版>/MmmTool/` へ写し、説明書類を `MmmTool_<版>/` に置く（配布物の場所は `DistributionDir`。試すときは `-p:DistributionDir=...` で変えられる）
  - 版をプロファイルの出力先に書かない理由: VS の「発行」は、発行プロファイルを単体で読んで出力先を決めるので、プロジェクトの `Version`（`Directory.Build.props`）が見えず、`MmmTool_\` になる。プロファイルで `Directory.Build.props` を `Import` すると、VS がプロファイルを読めなくなる（一覧から消える）。そのため、版はプロジェクトの中（`PackageDistribution`）で付ける外側の版のフォルダ（`MmmTool_<版>`）をそのままコピーして配布する。外側が版、内側がアプリ本体の形はユーザーの決定（`dotnet publish` の標準は発行フォルダの直下に出す形だが、手でコピーして配る運用に合わせた。アプリ本体だけをコピーすれば、コピー先のフォルダ名が版ごとに変わらない。版ごとにフォルダが分かれるので、前の版の控えも残る。`bin` の下にあるので、`bin` を丸ごと消すと一緒に消える点に注意する）。発行の前に既存のファイルを消す設定（`DeleteExistingFiles`）は使わない（ユーザーの決定）
- ビルドの出力先は `bin\<構成>\<TFM>\`、中間ファイルは `obj\<構成>\<TFM>\`（`Directory.Build.props` の `AppendPlatformToOutputPath=false`・`AppendRuntimeIdentifierToOutputPath=false`。プラットフォーム・RID は x64・win-x64 だけなので、段を作らない。SDK も同じ）
  - RID の段を作らない理由: VS の「発行」は、発行プロファイルの RID を参照先（SDK）にも渡し、参照先はビルドし直さない。段を作ると、発行のときだけ `...\win-x64\MmmSdk.WinUI.dll` を探しに行き、先にビルドした DLL（段なし）を見つけられずに失敗する
- 発行は Release でだけ行う（csproj の `EnsureReleaseForPublish`）。構成は、全プロジェクトに渡る形（VS の「発行」・`dotnet publish`・`-p:Configuration=Release`）で決める。発行プロファイルの `Configuration` だけに頼る呼び方（MSBuild を直接呼ぶなど）では、構成がこのプロジェクトにしか効かず、参照先が Debug でビルドされて配布物に入る。`Directory.Build.props` の Release の設定（`.pdb` を作らない）も効かない（プロファイルは `Directory.Build.props` より後に読まれる）。そのため、そのときはエラーで止める
- 発行プロファイルは `MmmTool/Properties/PublishProfiles/win-x64.pubxml` の 1 つだけで、リポジトリに入れる（`.gitignore` の `*.pubxml` から、このファイルだけを外している。フォルダーへの発行なので秘密の情報を含まない）。プロファイルに書くのは構成（Release）・プラットフォーム・RID・出力先（版を含まない `obj\Release\publish\`）のプロパティだけで（VS は `Import` などを書いたプロファイルを読めない）、コマンドの発行と同じ結果になるようにする。発行の設定の本体（ReadyToRun・トリミングなし・フレームワーク依存）は csproj の `Publish Properties` などに書き、プロファイルに重ねて書かない。MSIX 用のマニフェスト・ロゴは持たない（非パッケージで配布する）。`EnableMsixTooling` は、非パッケージでも WinUI のリソース生成に使うため true のまま残している
- 配布物の版のフォルダ（アプリ本体の 1 つ外側）には、配布用の説明書 `README.txt`（元は `MmmTool/Distribution/README.txt`。必要なもの・起動と終了・置き場所・データの保存場所・入れ替え・アンインストール・変更履歴・ライセンス）だけを置く。ライセンスの文書（`LICENSE.txt`（リポジトリ直下のもの。MIT）・`THIRD-PARTY-NOTICES.txt`）は、アプリ本体の `Assets\Licenses\` に入れる（アプリ本体だけをコピー・再配布されても、ライセンスが一緒に付いていくようにするため。EXE の横は増やさない。ユーザーの決定）。`THIRD-PARTY-NOTICES.txt` は、csproj の `GenerateThirdPartyNotices`（インラインタスク `GenerateThirdPartyNoticesFile`）が、発行のたびに、発行するファイルの一覧（`ResolvedFileToPublish`）から作る（手で書くと、パッケージの追加・版の更新でずれるため）。中身は、NuGet パッケージのライセンスファイル（無ければ、MIT なら nuspec の著作権表示で本文を作る）と第三者の通知（`NOTICE`・`ThirdPartyNotices`）、配布物の中の `名前.LICENSE.txt`（xterm.js など）。同じ本文のものは 1 つにまとめる。自分のプロジェクト（`MmmTool.Core`・MmmSdk）は含めない（`LICENSE.txt`）。説明書は csproj の `DistributionDocument` に並べ、`PackageDistribution` が発行のあとに版のフォルダへコピーする（ビルドの出力には入れない）。説明書をアプリ本体の外に置くのは、受け取った人が最初に開くフォルダに説明書とアプリ本体だけを並べるため（ユーザーの決定）。`README.txt` はメモ帳で開く前提で、UTF-8（BOM 付き）・CRLF にする（古いメモ帳でも文字化けしないように）
- リリースビルドには DEBUG ページ（コード・XAML）を含めない（csproj の条件付き `Remove`）
- WinUI の多言語リソース（言語名フォルダ内の `.mui`）は `SatelliteResourceLanguages` では消えないため、csproj の `PruneMuiAfterBuild` / `PruneMuiAfterPublish` で ja-JP・en-us 以外を削除する
- EXE の横のフォルダは `Assets`（配布物）・`Data`（アプリが書くもの）・`Lib`（パッケージの DLL）の 3 つだけにする（[decisions/0014-output-folders.md](decisions/0014-output-folders.md)・[decisions/0015-dll-reduction-and-lib.md](decisions/0015-dll-reduction-and-lib.md)）
  - `MmmTool.dll` 以外の DLL は `Lib` に置く（csproj の `MovePackageFilesToLib`・`MoveReferencesToLib`・`MoveRuntimePackFilesToLib`）。起動時に見つけられるよう、`MmmTool.deps.json` の各ファイルに `localPath` を書き足す（インラインタスク `AddLocalPathToDepsFile`）。EXE の横に残るのは `MmmTool.*` の 5 ファイル（exe・dll・deps.json・runtimeconfig.json・pri）と `Microsoft.Web.WebView2.Core.dll`（WinUI の WebView2 が EXE のフォルダから読むネイティブの部品）
  - Release では `.pdb` を出力に入れない（`Directory.Build.props`。Debug では入れる）。`.xml`（XML ドキュメントコメント）は、Debug・Release とも出力に入れない（検査のために obj には作る）
  - Windows App SDK の使わない部品（AI・ML・検索・ウィジェット）は出力に入れない。SDK は使う部品のパッケージだけを参照し、アプリは全部入りを参照したうえで使わない部品を `ExcludeAssets="all"` にする（CommunityToolkit が古い全部入りに依存しているため）。全部入りを上げるときは、`Directory.Packages.props` の部品の版も、新しい全部入りの依存に合わせる
  - ビルドも x64 専用（`RuntimeIdentifier=win-x64`。出力先のパスに RID は付けない。`Directory.Build.props`）。ネイティブ DLL は `runtimes\` ではなく EXE の横に置かれる
  - 画面の XAML（`.xbf`・SDK の `.xaml`）は `MmmTool.pri` の中に入る。WinUI のビルドがばらのファイルも出力へコピーするので、csproj の `RemoveLooseXamlAfterBuild` がビルドのあとに消す（中身が `.xbf`・`.xaml` だけのフォルダと、EXE の横の `.xbf`）。発行の出力には、もともと出ない
  - WebView2 のキャッシュは `Data\WebView2`、エラーのログは `Data\Logs`（`Shell/AppInfo`）
- 同梱の xterm.js 6.0.0 / addon-fit 0.11.0（MIT）は SDK の `MmmSdk.WinUI/Components/Terminal/Assets/`（ライセンスファイルも同じ場所）。SDK の csproj が、出力・発行フォルダーの `Assets/Terminal/` へ配る

## C# の書き方
- ロックは `System.Threading.Lock`
- 値の変換は `IValueConverter` ではなく `x:Bind` の関数呼び出し（添付のサムネイルは SDK の `ThumbnailImage.FromFile`、完了・削除済みの見た目は `Features/Reminders/ReminderRowStyle`）
- エラー表示: ViewModel は SDK の `ErrorState` を `Error` として 1 つ持ち、`InfoBar` の `IsOpen`（TwoWay）と `Message` に結び付ける（閉じる処理は書かない）。ファイルの読み込み結果は SDK の `LoadStatus` で持つ
- ウィンドウの共通の設定（アイコン + タイトルバー・最大化/最小化なしの枠・大きさ・位置合わせ）は SDK の `Window` 拡張メソッド（`UseCustomTitleBar` など）。アイコンのパスは `Shell/AppIcon`（ウィンドウ・トレイで共通）。アプリの名前・Data フォルダー（とその下の Logs・WebView2）・トレイのクラス名は `Shell/AppInfo` の 1 か所（多重起動の防止・エラーのダイアログ・添付の一時フォルダーでも使う）
- リマインダーのメイン画面・一覧画面の ViewModel は、共通の骨格（保存内容の変更の購読・読み込みの世代管理・保存の失敗のエラー化）を `Features/Reminders/ReminderViewModelBase` に持つ
- 受け取って持つだけのクラスはプライマリコンストラクタ。コンストラクタの中に初期化の処理があるものは従来の形
- ターミナルの後始末（SDK の `PseudoConsoleSession` の `Dispose`）は同期のまま。`IAsyncDisposable` にすると DI コンテナが `ConfigureAwait(false)` で待ち、後から破棄されるトレイアイコンなどが UI スレッドの外で破棄されるため
- JSON は `System.Text.Json` のソース生成（トリミングは今は無効だが、戻しても動く形を保つ）
- コメントは XML ドキュメントコメント（`<summary>` は短く、長い説明は `<remarks>`）。引数・戻り値も書く

## 開発時の注意
- VS の F5 で通知ダイアログを閉じると `Microsoft.UI.Xaml.dll` 内で `0xC000027B` / `E_UNEXPECTED` で落ちることがある。原因は VS の「XAML 診断」（オプション → デバッグ → XAML 診断）で、オフにすると解消する（Ctrl+F5 や VS なしでは起きない）。開発時はオフにしておく
- 一時ファイル（作業用ファイル等）はリポジトリ直下の `_local/` に置く（`.gitignore` 済み）
- 既定の定型コマンドを増やしても、生成済みの `Data/CliCommands.json` には反映されない（無いときだけ生成する）。反映するにはアプリを閉じて JSON を削除する
