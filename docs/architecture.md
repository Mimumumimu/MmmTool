# 全体構成

## プロジェクト
| プロジェクト | 対象 | 役割 |
| --- | --- | --- |
| `MmmTool` | `net10.0-windows10.0.19041.0`（WinUI 3） | アプリ本体（画面・UI サービス・Win32 呼び出し） |
| `MmmTool.Core` | `net10.0` | UI に依存しない処理（Entity・Repository・サービス）。Windows / WinUI を参照しない |
| `external/MmmSdk/MmmSdk.Core` | `net10.0` | 共有部品（JSON の保存・設定ストア・位置保存・パスを開く） |
| `external/MmmSdk/MmmSdk.WinUI` | `net10.0-windows10.0.19041.0` | 共有部品の UI（通知ダイアログ） |

参照の向きは `MmmTool → MmmTool.Core → MmmSdk.Core`、`MmmTool → MmmSdk.WinUI → MmmSdk.Core`。SDK はアプリを知らない。SDK は別リポジトリ（`https://github.com/Mimumumimu/MmmSdk`）で、Git サブモジュール `external/MmmSdk` として取り込む（clone は `--recurse-submodules`、取りこぼしたら `git submodule update --init --recursive`）。SDK を直したら、サブモジュールの中（master）でコミット・push してから、アプリ側で「新しいコミットを指す」コミットをする（SDK が先）。

何をどこに置くか：UI・Windows に依存しない処理はアプリの Core、どのアプリでも使える汎用の部品は SDK、アプリ固有の画面・機能はアプリ本体。

## フォルダ（機能別）
プロジェクトの中は機能ごとのフォルダ（Vertical Slice）。層ごとのフォルダ（`Views/` `ViewModels/` 等）は作らない。名前空間はフォルダどおり。MVVM の役割はクラス名で分かるようにする（View = `*Page` / `*Window`、ViewModel = `*ViewModel`、Model = Core の Entity・サービス）。決めた理由は [decisions/0001-feature-folders.md](decisions/0001-feature-folders.md)。

```
MmmTool.Core/<機能>/            Entity・Repository のインターフェース・サービス（CliAssist / Links / Reminders）
MmmTool.Core/<機能>/Json/       JSON の実装（Json<名前>Repository）と機能ごとのソース生成 Context
MmmTool/Features/<機能>/        View・ViewModel・行の型・UI サービス・トレイの項目・Add<機能>()・<機能>Startup
                                （CliAssist / Links / Reminders / Settings / Debugging）
                                機能の中が大きいときは、責務ごとのサブフォルダーに分ける（名前空間もフォルダーどおり）:
                                CliAssist/{Terminal, WorkingDirectory, Attachments}、Reminders/{Main, Input, List}
MmmTool/Shell/                  画面の枠（MainWindow・MainViewModel・NavigationItem / NavigationPage / NavigationArea・
                                PageProvider・IStartupTask・ShellServiceCollectionExtensions）
```

- `Shell/` は特定の機能を参照しない（機能から共通部分への一方向）。機能固有の画面を開く口は、その機能に置く
- DEBUG 用の機能のフォルダ名は `Debugging`（`Debug` にすると `System.Diagnostics.Debug` を隠すため）

## DI と起動
- Generic Host（`Host.CreateApplicationBuilder`）で DI を組む。`App.ConfigureServices` は「SDK → `AddShell()` → 各機能の `Add<機能>()`」を呼ぶだけ
- 機能を登録した順（CLI補助 → リマインダー → リンク → DEBUG → 設定）に、サイドバーの項目（上部・下部それぞれ）・トレイメニューの項目（リマインダーがリンクより上）・起動時の準備が並ぶ
- 保存先（Repository の実装）は各 `Add<機能>()` の「保存先」の行。CLI補助・リンクはローカル専用。DB に替えるなら、リマインダーなど該当機能の行を差し替える
- サイドバー: 各機能が `AddNavigationPage<TPage>(表示名, グリフ, 上部/下部)` で登録する（ページは Transient・キーは型名）。`MainViewModel` が登録から項目を作り、`MainWindow` は `PageProvider`（初回に DI から作ってキャッシュ）からページを受け取る。DEBUG は `AddDebugging()` の中の `#if DEBUG` で、リリースでは登録しない
- 起動時の準備（`IStartupTask`）: `App.OnLaunched` で、`TrayIcon` を解決したあと・`MainWindow` を作る前に、UI スレッドで登録順に `StartAsync` を待つ（CLI補助の利用状態の読み込み → リマインダー監視の開始 → リンクの先読み）。決めた理由は [decisions/0002-startup-task.md](decisions/0002-startup-task.md)
- ダイアログ: 共通の `IDialogService`（SDK）は確認ダイアログだけ。機能固有の画面は各機能の口から開く（`IReminderDialogService.ShowInputAsync` / `ShowListAsync`、`IWorkingDirectoryDialogService.ShowAsync`）。実装は SDK の `IDialogHost` の `Owner`（親の決定）と `ShowModalAsync`（開いている間モーダルとして覚える）を使う（具象の `DialogService` には依存しない）。ピッカーの親も `IDialogHost.Owner`
- 終了の順序: `App.ExitAsync` で `MainWindow.PrepareExit`（閉じる要求を素通しにする）→ Host 停止・破棄 → `Exit()`。DI は作った順の逆に破棄するので、`TrayIcon` を画面・各機能（起動時の準備を含む）より先に解決しておき、各機能の後始末のあとにトレイアイコンが消えるようにしている

## エラーの扱い
3 段階。予測できる失敗は、先に確かめる（`TryXxx`・検証・ガード節）。それでも起きる失敗（ファイル・JSON・OS・COM）は、範囲を絞った `catch` で受けて画面に出す（InfoBar）。復旧が難しい失敗・予想外の失敗（バグ）は、ログ（`AppContext.BaseDirectory/Logs/yyyy-MM-dd.log`）→ ダイアログ → 終了（SDK の `FatalErrorHandler`）。隠さず、握りつぶさない。
- `App` のコンストラクターで `FatalErrorHandler` を作って `AttachTo(this)` し（Host を作る前の失敗も拾うため）、DI にも登録する（トレイが使う）
- `try/catch` を書けない場所（`async void`・タイマー・待たれないタスク。例: `ReminderMonitor.OnTick`）の例外は、`AttachTo` の安全網が、同じ処理（ログ → ダイアログ → 終了）で受ける
- 待たずに走らせるタスクは `_ = SomeAsync();` で捨てず、`SomeAsync().Forget()`（SDK の `MmmSdk.Core.Tasks`）にする。捨てると、失敗が誰にも見えず、ガベージコレクションのときに初めて分かる（いつ落ちるか読めない）。`Forget` は、失敗した時点で未処理例外にして、安全網が受ける。起きると分かっている失敗（`DataFileException` など）は、タスクの中で受けて画面に出す（例: `SettingsViewModel.SaveSnoozeIntervalAsync`、`ReminderViewModelBase.RunAsync`）
- `OnLaunched` は、起動が途中で止まって気づけなくならないよう、`try/catch` で包んで報告する。読み込みの失敗など、起動を止めたくない例外は、各機能の `IStartupTask` の中で受ける
- トレイの `WndProc` は、`[UnmanagedCallersOnly]` から例外が抜けるとログも残らず落ちるため、中で `try/catch` して報告する。異常終了のときは、トレイのアイコンも外す
- 詳細は `external/MmmSdk/README.md`「エラーのログと、復旧できないエラーの処理」

## 保存
JSON。場所は `AppContext.BaseDirectory/Data/*.json`。手で修正するときはアプリを閉じてから行う。保存先は将来 SQL Server / DynamoDB などに替える可能性があり、Repository + DI で差し替えられる形にしている。詳細は [specs/storage.md](specs/storage.md)。

## ビルドの共通設定
- リポジトリ直下に `Directory.Build.props`（バージョン・Nullable・ImplicitUsings・`GenerateDocumentationFile`・`EnforceCodeStyleInBuild`・XML ファイルを発行物に含めない）、`Directory.Packages.props`（中央パッケージ管理。csproj の `PackageReference` にはバージョンを書かない）、`.editorconfig`（`root = true`）を置く。3 つとも slnx の「Solution Items」に入れている
- `.editorconfig`: 未使用 using・ファイル単位の名前空間・using の位置・複数行の本体の波かっこを warning にしてビルドで検査する。1 行の早期 return（`if (x) return;`）は波かっこを省略してよい。`charset` は書かない（BOM 付きの ps1 があるため）
- SDK はリポジトリ直下に自分の同じ 1 組を持つ。MSBuild・.editorconfig は近いほうを使うので、アプリと SDK の設定は混ざらない。共通のパッケージ（CommunityToolkit.Mvvm・WindowsAppSDK・SDK.BuildTools）は SDK を先に上げて、アプリを同じバージョンにする
- バージョンは `Version`（現在 0.1.0。ファイル・アセンブリのバージョンは自動で 0.1.0.0）。製品バージョンの後ろにはコミット番号が付く（.NET の標準の動作）
- XML コメントの検査と未使用 using の検査は、普通の `dotnet build` / VS のビルドでかかる

## 配布
- フレームワーク依存（`SelfContained=false` / `WindowsAppSDKSelfContained=false`）。実行する PC に .NET 10 Desktop Runtime と Windows App Runtime 2.5 が必要。決めた理由は [decisions/0003-framework-dependent.md](decisions/0003-framework-dependent.md)
- 発行は `dotnet publish .\MmmTool\MmmTool.csproj -c Release -p:Platform=x64`。出力フォルダをそのままコピーして配布する
- 発行プロファイル（`.pubxml`）は使わない（`.gitignore` で除外されており、clone 直後に再現できないため）。設定は csproj の `Publish Properties` に書く。MSIX 用のマニフェスト・ロゴは持たない（非パッケージで配布する）。`EnableMsixTooling` は、非パッケージでも WinUI のリソース生成に使うため true のまま残している
- リリースビルドには DEBUG ページ（コード・XAML）を含めない（csproj の条件付き `Remove`）
- WinUI の多言語リソース（言語名フォルダ内の `.mui`）は `SatelliteResourceLanguages` では消えないため、csproj の `PruneMuiAfterBuild` / `PruneMuiAfterPublish` で ja-JP・en-us 以外を削除する
- `MmmTool.exe.WebView2`（WebView2 のキャッシュ）は起動時に exe の隣へ作られる実行時データで、1 フォルダなので放置する
- 同梱の xterm.js 6.0.0 / addon-fit 0.11.0（MIT）は `Assets/Terminal/`。ライセンスファイルも同梱

## C# の書き方
- ロックは `System.Threading.Lock`
- 値の変換は `IValueConverter` ではなく `x:Bind` の関数呼び出し（添付のサムネイルは `Features/CliAssist/Attachments/ThumbnailImage.FromFile`）
- エラー表示: ViewModel は SDK の `ErrorState` を `Error` として 1 つ持ち、`InfoBar` の `IsOpen`（TwoWay）と `Message` に結び付ける（閉じる処理は書かない）。ファイルの読み込み結果は SDK の `LoadStatus` で持つ
- ウィンドウの共通の設定（アイコン + タイトルバー・最大化/最小化なしの枠・大きさ・位置合わせ）は SDK の `Window` 拡張メソッド（`UseCustomTitleBar` など）。アイコンのパスは `Shell/AppIcon`（ウィンドウ・トレイで共通）
- リマインダーのメイン画面・一覧画面の ViewModel は、共通の骨格（保存内容の変更の購読・読み込みの世代管理・保存の失敗のエラー化）を `Features/Reminders/ReminderViewModelBase` に持つ
- 受け取って持つだけのクラスはプライマリコンストラクタ。コンストラクタの中に初期化の処理があるものは従来の形
- ターミナルの後始末（`PseudoConsoleSession.Close`）は同期のまま。`IAsyncDisposable` にすると DI コンテナが `ConfigureAwait(false)` で待ち、後から破棄されるトレイアイコンなどが UI スレッドの外で破棄されるため
- JSON は `System.Text.Json` のソース生成（トリミングは今は無効だが、戻しても動く形を保つ）
- コメントは XML ドキュメントコメント（`<summary>` は短く、長い説明は `<remarks>`）。引数・戻り値も書く

## 開発時の注意
- VS の F5 で通知ダイアログを閉じると `Microsoft.UI.Xaml.dll` 内で `0xC000027B` / `E_UNEXPECTED` で落ちることがある。原因は VS の「XAML 診断」（オプション → デバッグ → XAML 診断）で、オフにすると解消する（Ctrl+F5 や VS なしでは起きない）。開発時はオフにしておく
- 一時ファイル（作業用ファイル等）はリポジトリ直下の `_local/` に置く（`.gitignore` 済み）
- 既定の定型コマンドを増やしても、生成済みの `Data/CliCommands.json` には反映されない（無いときだけ生成する）。反映するにはアプリを閉じて JSON を削除する
