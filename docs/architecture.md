# 全体構成

## プロジェクト
| プロジェクト | 対象 | 役割 |
| --- | --- | --- |
| `MmmTool` | `net10.0-windows10.0.19041.0`(WinUI 3) | ホスト (メインウィンドウ・サイドバー・トレイ・機能のオン・オフ・設定ページ・DEBUG ページ)。機能のプラグインを参照して登録する |
| `Plugins/MmmTool.<機能>.Core` | `net10.0` | 機能の、UI に依存しない処理 (Entity・Repository・サービス)。Windows / WinUI を参照しない。機能は Backlog / CliAssist / ClipboardTransfer / Links / Reminders の 5 つ |
| `Plugins/MmmTool.<機能>` | `net10.0-windows10.0.19041.0`(WinUI 3 のライブラリ) | 機能の画面・ViewModel・DI 登録の入口 (`<機能>Plugin`)・機能が持つ資材 (CLI補助の補助スクリプト) |
| `Shared/MmmTool.Users.Core` | `net10.0` | 機能をまたいで共有する、ユーザー (`AppUser`・今のユーザー `CurrentUser`・この PC の MAC からの特定)。画面は持たない。DB の保存先 (`IAppUserRepository`)のインターフェースだけを持ち、実装は `MmmTool.Data.<種類>` |
| `Data/MmmTool.Data` | `net10.0` | DB の種類に依存しない部分 (DB の行のクラスと、アプリの型との変換・接続の設定)。全機能の分をここに集める。参照するのは、DB に保存する機能の `.Core`(Reminders)と `MmmTool.Users.Core` |
| `Data/MmmTool.Data.<種類>` | `net10.0` | その DB の Repository の実装 (`MmmTool.Data.SqlServer`)。SDK の `MmmSdk.Db.<種類>` を参照する。ホストが設定で DB を選んだときだけ、機能が登録した JSON の Repository を置き換える |
| `external/MmmSdk/MmmSdk.Db.<種類>` | `net10.0` | DB への接続の部品 (`MmmSdk.Db.SqlServer`)。外部のドライバーを参照するので、`MmmSdk.Core` とは別のプロジェクト ([SDK の db-sqlserver.md](../external/MmmSdk/docs/db-sqlserver.md)) |
| `external/MmmSdk/MmmSdk.Core` | `net10.0` | 共有部品のうち UI に依存しないもの (機能がホストへ入る口 (`Features`・`Hosting`)・JSON の保存・設定ストア・秘密の保存の口・位置保存・パスを開く・通知の項目・毎分のスケジューラー・添付の一時保存・多重起動の防止・エラーログ・`Forget`) |
| `external/MmmSdk/MmmSdk.WinUI` | `net10.0-windows10.0.19041.0` | 共有部品のうち UI・Windows に依存するもの (サイドバーのページ・設定の部品の登録 (`Pages`)・通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択・資格情報マネージャーでの秘密の保存・擬似モーダル・タスクトレイ・ConPTY・コントロール・IME・ウィンドウ補助・エラー処理。Win32 の宣言は CsWin32 でここに集める) |

参照の向きは `MmmTool → MmmTool.<機能> → MmmTool.<機能>.Core → MmmSdk.Core`、`MmmTool.<機能> → MmmSdk.WinUI → MmmSdk.Core`。機能のプラグインどうしは参照しない。DB に保存するときの参照の向きは `MmmTool.Data.<種類> → MmmTool.Data → MmmTool.<機能>.Core`(`MmmTool.Data.<種類>` は `MmmSdk.Db.<種類>` も参照する)で、機能のライブラリは `MmmTool.Data*` を参照しない。機能をまたぐユーザー (`AppUser`)は、機能のライブラリでも `MmmTool.Data` でもなく、`MmmTool.Users.Core` に置き、`MmmTool.Data` と、ユーザーを使う機能の `.Core` が参照する ([決定の理由](#db-のコードは機能とは別の-mmmtooldata-にまとめる))。機能はホスト (`MmmTool`)を参照しない (ホストに頼る値は、SDK の `AppEnvironment`・`IPageCache`・`IFeatureStatus` を DI から受け取る)。SDK はアプリを知らない。SDK は別リポジトリ (`https://github.com/Mimumumimu/MmmSdk`)で、Git サブモジュール `external/MmmSdk` として取り込む (clone は `--recurse-submodules`、取りこぼしたら `git submodule update --init --recursive`)。SDK を直したら、サブモジュールの中 (master)でコミット・push してから、アプリ側で「新しいコミットを指す」コミットをする (SDK が先)。

何をどこに置くか：機能固有の UI・Windows に依存しない処理は機能の `.Core`、機能固有の画面は機能のライブラリ、どのアプリでも使える汎用の部品 (機能がホストへ入る口を含む)は SDK、機能を載せる枠はアプリ本体 (`MmmTool`)。

## フォルダ (機能別)
プロジェクトの中は機能ごとのフォルダ (Vertical Slice)。層ごとのフォルダ (`Views/` `ViewModels/` 等)は作らない。名前空間はフォルダどおり。MVVM の役割はクラス名で分かるようにする (View = `*Page` / `*Window`、ViewModel = `*ViewModel`、Model = Core の Entity・サービス)。決めた理由は下の「決定の理由」の「機能別フォルダ」。

```
Plugins/MmmTool.<機能>.Core/          Entity・Repository のインターフェース・サービス (Backlog / CliAssist / ClipboardTransfer / Links / Reminders)
Plugins/MmmTool.<機能>.Core/Json/     JSON の実装 (Json<名前>Repository)と機能ごとのソース生成 Context
Plugins/MmmTool.<機能>/               機能全体のつなぎ (<機能>Plugin・<機能>Startup・<機能>TrayMenuSource など、ホストへ登録するもの)と、
                                      複数の画面で共有するもの (ViewModel の基底クラス・行の書式・I<機能>DialogService)だけ
Plugins/MmmTool.<機能>/Main/         その機能の入口の画面 (サイドバーのページ、またはトレイから開くメインのウィンドウ)の
                                      View・ViewModel・行の型。画面が 1 つだけの機能も Main/ に入れる
Plugins/MmmTool.<機能>/<画面>/       そのほかの画面 (Reminders/{Input, List, Settings}、CliAssist/WorkingDirectory)。
                                      1 画面 = 1 フォルダ。その画面の View・ViewModel・行の型・その画面だけのサービスを一緒に置く
Shared/MmmTool.Users.Core/            機能をまたいで共有するユーザー (AppUser・CurrentUser・UserIdentificationService・IMacAddressProvider)
Data/MmmTool.Data/Connection/         DB への接続の設定 (DatabaseSettings・DatabaseSettingsService。機能をまたぐので、機能のフォルダではない)
Data/MmmTool.Data/<機能>/             DB の行のクラス (<名前>Row)と、アプリの型との変換 (全機能の分。機能ごとのフォルダ)
Data/MmmTool.Data.<種類>/Connection/  その DB への接続を作る処理 (SqlServerConnectionFactoryBuilder。設定とパスワードから SDK の接続の部品を作る)
Data/MmmTool.Data.<種類>/<機能>/      その DB の Repository の実装 (<種類><名前>Repository)
MmmTool/Features/<機能>/              ホストが持つ機能 (Settings / Debugging / Users。Users は DB モードのユーザーの特定と、登録の画面)。中は上と同じ形 (つなぎ + Main/ + 画面ごとのフォルダ)
MmmTool/Shell/                        画面の枠 (ホスト側)。直下に DI 登録と、アプリ全体で使う型
                                     (SettingsStoreStartup・
                                      AppIcon・AppInfo・ShellServiceCollectionExtensions)
MmmTool/Shell/Main/                   メインウィンドウ (MainWindow・MainViewModel・PageProvider)
```

- 機能 (プラグイン)がホストへ入るときに使う型 (`IFeaturePlugin`・`FeatureInfo`・`IStartupTask`・`IFeatureDisableConfirmation`・`IFeatureStatus`・`NavigationPage`・`SettingsSection`・`IReleasablePage`・`IPageCache`・`AppEnvironment`・`AddFeature` / `AddStartupTask` / `AddNavigationPage` / `AddSettingsSection` / `AddTrayMenuSource`)は、SDK の `MmmSdk.Core.Components.Features` / `.Hosting`、`MmmSdk.WinUI.Components.Pages` / `.Tray` にある。機能もホストも、それを参照する
- 名前空間はフォルダどおりで、機能の UI は `MmmTool.<機能>`(例: `MmmTool.Links.Main`)、`.Core` は `MmmTool.<機能>.Core`(例: `MmmTool.Links.Core.Json`)
- 機能が持つ資材 (CLI補助の補助スクリプト `Tools/`)は、機能のライブラリに置き、csproj の `None` の `Link` で、アプリの出力の `Assets\Tools\` へ配る (WinUI のライブラリの `Content` と、`Assets` フォルダーの中身は、コピー先にプロジェクト名のフォルダーが付くため。SDK のターミナルの資材と同じ形)

- 共有ライブラリ (`external/MmmSdk`)のフォルダは、アプリとは別に、プロジェクトの直下を `Components/<部品>/`・`Controls/`・`Utilities/` の 3 層に分けている ([SDK の architecture.md](../external/MmmSdk/docs/architecture.md))。アプリの `Features/<機能>/` には同じ層を作らない
- フォルダの決まり (機能の直下 = つなぎ、`Main/` = 入口の画面、`<画面>/` = 1 画面 1 フォルダ)の理由は下の「決定の理由」の「機能のフォルダの形」。Core には同じ形を当てない (画面が無い)
- `Shell/` は特定の機能を参照しない (機能から共通部分への一方向)。機能固有の画面を開く口は、その機能に置く
- DEBUG 用の機能のフォルダ名は `Debugging`(`Debug` にすると `System.Diagnostics.Debug` を隠すため)

## DI と起動
- Generic Host (`Host.CreateApplicationBuilder`。`DisableDefaults = true` で、使わない設定 (appsettings.json・環境変数)とロガーの既定は無効)で DI を組む。ログは SDK の `ErrorLog`(エラーのファイル)だけで、`ILogger` は使わない。`App.ConfigureServices` は「SDK → `AddShell()` → 各機能 (`AddFeaturePlugin<<機能>Plugin>()`)」を呼ぶだけ
- SDK の DI 登録は、`AddMmmSdkCore(dataDirectory)`(JSON の保存・設定ストア・位置保存・パスを開く処理。先に登録する)→ `AddMmmSdkWinUI()`(通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択・クリップボード・秘密の保存・読み上げ)の順 (`App.ConfigureServices`)。アプリ固有の Entity・Repository は `MmmTool.Core` に残す
- 機能を登録した順 (CLI補助 → クリップボード転送 → リマインダー → Backlog → リンク → DEBUG → 設定)に、サイドバーの項目 (上部・下部それぞれ)・トレイメニューの項目 (リマインダーがリンクより上)・起動時の準備が並ぶ
- 開くたびに作るウィンドウ (`IDisposable` の ViewModel を持つもの)は、`IServiceScopeFactory` で作ったスコープから解決し、閉じたらスコープを破棄する (ルートのプロバイダーから解決した `IDisposable` の Transient は、Host の破棄まで保持され続けるため)。サイドバーのページも、ページごとのスコープから解決する (`PageProvider`。オフにできる機能のページは、オフにしたときにスコープを破棄し、`PseudoConsoleSession` などを解放する。残りは Host の破棄で解放する)。常駐するもの (リマインダーの監視など)は、Host の破棄で `Dispose` されることを前提に、ルートから解決する
- 保存先 (Repository の実装)は各機能の入口 (`<機能>Plugin.Register`)の「保存先」の行。CLI補助・リンクはローカル専用。DB に替えるなら、リマインダーなど該当機能の行を差し替える
- 設定ページ: 各機能が `AddSettingsSection<TControl>()` で設定の部品を登録し、設定ページは登録順に並べるだけ ([specs/settings.md](specs/settings.md))
- 機能のオン・オフ: オフにできる機能 (今は CLI補助・クリップボード転送・Backlog)は、`<機能>Plugin.Register` の中で `AddFeature(キー, 表示名)` を登録し、ページ・起動時の準備・トレイメニュー・設定の部品の登録に同じキーを渡す (`AddNavigationPage` / `AddStartupTask` / `AddTrayMenuSource` / `AddSettingsSection` の最後の引数。省略はオフにできない機能)。`FeatureService`(Shell)が状態の保存・起動時の準備の実行・切り替えの通知 (`Changed`)を持つ。Shell は機能の名前を知らず、キーで絞り込むだけ (理由は [specs/settings.md](specs/settings.md) の「決定の理由」)
- サイドバー: 各機能が `AddNavigationPage<TPage>(表示名, グリフ, 上部/下部)` で登録する (ページは Transient・キーは型名)。`MainViewModel` が登録から項目を作り、`MainWindow` は `PageProvider`(初回に DI から作ってキャッシュ)からページを受け取る。DEBUG は `AddDebugging()` の中の `#if DEBUG` で、リリースでは登録しない
- 起動時の準備 (`IStartupTask`): `App.OnLaunched` で、`TrayIcon` を解決したあと・`MainWindow` を作る前に、UI スレッドで `FeatureService.StartAsync` が、オンの機能の分だけ登録順に待つ (共通の設定ファイルの先読み → CLI補助の利用状態の読み込み → リマインダー監視の開始 → リンクの先読み)。決めた理由は下の「決定の理由」の「起動時の準備」
- ダイアログ: 共通の `IDialogService`(SDK)は確認ダイアログだけ。機能固有の画面は各機能の口から開く (`IReminderDialogService.ShowInputAsync` / `ShowListAsync`、`IWorkingDirectoryDialogService.ShowAsync`)。実装は SDK の `IDialogHost` の `Owner`(親の決定)・`ShowModalAsync`(開いている間モーダルとして覚える)・`Attach`(`ContentDialog` に親の画面とテーマを渡す)を使う (具象の `DialogService` には依存しない)。ピッカーの親も `IDialogHost.Owner`
- 終了の順序: `App.ExitAsync` で `MainWindow.PrepareExit`(閉じる要求を素通しにする)→ Host 停止・破棄 → `Exit()`。DI は作った順の逆に破棄するので、`TrayIcon` を画面・各機能 (起動時の準備を含む)より先に解決しておき、各機能の後始末のあとにトレイアイコンが消えるようにしている

## エラーの扱い
3 段階。予測できる失敗は、先に確かめる (`TryXxx`・検証・ガード節)。それでも起きる失敗 (ファイル・JSON・OS・COM)は、範囲を絞った `catch` で受けて画面に出す (InfoBar)。復旧が難しい失敗・予想外の失敗 (バグ)は、ログ (`AppContext.BaseDirectory/Data/Logs/yyyy-MM-dd.log`)→ ダイアログ → 終了 (SDK の `FatalErrorHandler`)。隠さず、握りつぶさない。
- `App` のコンストラクターで `FatalErrorHandler` を作って `AttachTo(this)` し (Host を作る前の失敗も拾うため)、DI にも登録する (トレイが使う)
- `try/catch` を書けない場所 (`async void`・タイマー・待たれないタスク。例: SDK の `MinuteScheduler` の毎分の処理)の例外は、`AttachTo` の安全網が、同じ処理 (ログ → ダイアログ → 終了)で受ける
- 待たずに走らせるタスクは `_ = SomeAsync();` で捨てず、`SomeAsync().Forget()`(SDK の `MmmSdk.Core.Utilities`)にする。捨てると、失敗が誰にも見えず、ガベージコレクションのときに初めて分かる (いつ落ちるか読めない)。`Forget` は、失敗した時点で未処理例外にして、安全網が受ける。起きると分かっている失敗 (`DataFileException` など)は、タスクの中で受けて画面に出す (例: `SettingsViewModel.SaveSnoozeIntervalAsync`、`ReminderViewModelBase.RunAsync`)
- `OnLaunched` は、起動が途中で止まって気づけなくならないよう、`try/catch` で包んで報告する。読み込みの失敗など、起動を止めたくない例外は、各機能の `IStartupTask` の中で受ける
- トレイの `WndProc` は、`[UnmanagedCallersOnly]` から例外が抜けるとログも残らず落ちるため、中で `try/catch` して報告する。異常終了のときは、トレイのアイコンも外す
- 詳細は `external/MmmSdk/README.md`「エラーのログと、復旧できないエラーの処理」

## 保存
JSON。場所は `AppContext.BaseDirectory/Data/*.json`。手で修正するときはアプリを閉じてから行う。保存先は将来 SQL Server / DynamoDB などに替える可能性があり、Repository + DI で差し替えられる形にしている。詳細は [specs/storage.md](specs/storage.md)。

## C# の書き方
- ロックは `System.Threading.Lock`
- 値の変換は `IValueConverter` ではなく `x:Bind` の関数呼び出し (添付のサムネイルは SDK の `ThumbnailImage.FromFile`、完了・削除済みの見た目は `Features/Reminders/ReminderRowStyle`)
- エラー表示: ViewModel は SDK の `ErrorState` を `Error` として 1 つ持ち、`InfoBar` の `IsOpen`(TwoWay)と `Message` に結び付ける (閉じる処理は書かない)。ファイルの読み込み結果は SDK の `LoadStatus` で持つ
- ウィンドウの共通の設定 (アイコン + タイトルバー・最大化/最小化なしの枠・大きさ・位置合わせ)は SDK の `Window` 拡張メソッド (`UseCustomTitleBar` など)。アイコンのパスは `Shell/AppIcon`(ウィンドウ・トレイで共通)。アプリの名前・Data フォルダー (とその下の Logs・WebView2)・トレイのクラス名は `Shell/AppInfo` の 1 か所 (多重起動の防止・エラーのダイアログ・添付の一時フォルダーでも使う)
- リマインダーのメイン画面・一覧画面の ViewModel は、共通の骨格 (保存内容の変更の購読・読み込みの世代管理・保存の失敗のエラー化)を `Features/Reminders/ReminderViewModelBase` に持つ
- 受け取って持つだけのクラスはプライマリコンストラクタ。コンストラクタの中に初期化の処理があるものは従来の形
- ターミナルの後始末 (SDK の `PseudoConsoleSession` の `Dispose`)は同期のまま。`IAsyncDisposable` にすると DI コンテナが `ConfigureAwait(false)` で待ち、後から破棄されるトレイアイコンなどが UI スレッドの外で破棄されるため
- JSON は `System.Text.Json` のソース生成 (トリミングは今は無効だが、戻しても動く形を保つ)
- コメントは XML ドキュメントコメント (`<summary>` は短く、長い説明は `<remarks>`)。引数・戻り値も書く

## 開発時の注意
- VS の F5 で通知ダイアログを閉じると `Microsoft.UI.Xaml.dll` 内で `0xC000027B` / `E_UNEXPECTED` で落ちることがある。原因は VS の「XAML 診断」 (オプション → デバッグ → XAML 診断)で、オフにすると解消する (Ctrl+F5 や VS なしでは起きない)。開発時はオフにしておく
- 一時ファイル (作業用ファイル等)はリポジトリ直下の `_local/` に置く (`.gitignore` 済み)
- 既定の定型コマンドを増やしても、生成済みの `Data/CliCommands.json` には反映されない (無いときだけ生成する)。反映するにはアプリを閉じて JSON を削除する

## 決定の理由
上の各項目を「なぜそう決めたか」。大きな決定をしたら、該当する文書のこの見出しに足す (機能の決定はその機能の `specs/*.md`、SDK の決定は SDK の `docs/`)。

### 機能別フォルダ
- 層ごとのフォルダ (`Views/` `ViewModels/` `Entities/`)にすると、1 つの機能を触るたびに複数のフォルダを行き来し、機能が増えるほど関係するファイルが散らばる。機能ごとに View・ViewModel・Entity・サービスを同じフォルダに置けば、機能を足す・直すときに触るのは、その機能のフォルダと `App` の 1 行で済む
- 保存先を替えるときも、その機能の入口 (`<機能>Plugin.Register`)の 1 行を差し替えるだけで済む
- MVVM の役割は、フォルダではなくクラス名で分かるようにする
- 機能をまたぐものは、アプリ固有なら `Shell/`、汎用なら SDK に置き、どちらも特定の機能を参照しない (機能 → 共通部分の一方向)。サイドバーのページ・トレイメニューの項目・起動時の準備も、機能側から登録する
- 影響: フォルダ名が名前空間になる。XAML で同じ名前空間の型は `local:` で参照する。名前空間がよく使う型名を隠さないよう、フォルダ名に注意する (例: `Debug` ではなく `Debugging`)

### 機能のフォルダの形
- 入口の画面の置き場所が機能ごとに違い (直下の機能と `Main/` の機能が混ざっていた)、「機能全体のつなぎ」と画面の View・ViewModel が同じ段に並んで見分けにくかった。どの機能 (と `Shell/`)も同じ形 (つなぎ + `Main/` + 画面ごとのフォルダ)にそろえた
- 画面が 1 つだけの機能も `Main/` に入れる理由: フォルダが 1 段増えても、どの機能も同じ形にそろえることを優先する (1 つを覚えれば全部が読める)
- Core に同じ形を当てない理由: Core には画面が無く、中身は「その機能のデータと処理」の 1 種類だけ (保存先の実装は `Json/` に分かれている)
- XAML では、移した画面から、親のフォルダ (機能の直下)に残った型を、`local:` ではなく親の名前空間の xmlns で参照する (例: `MainWindow.xaml` の `pages:NavigationItem`)
- 最終形にどう近づくか: 新しい機能・画面を足すときの置き場所が、迷わず決まる

### 機能を、プラグインのライブラリに分ける
- 機能 (リンク・Backlog・クリップボード転送・リマインダー・CLI補助)を、機能ごとのライブラリ (`Plugins/MmmTool.<機能>` と、UI に依存しない `.Core`)に分け、ホスト (`MmmTool`)は、使う機能のライブラリを参照して `AddFeaturePlugin<<機能>Plugin>()` で登録する。必要な機能だけを参照すれば、その機能だけを持つ配布物になる
- 機能がホストへ入るときの型 (`IFeaturePlugin`・サイドバーのページ・設定の部品・起動時の準備・機能のオン・オフ・トレイメニューの登録の口)は、SDK に置く。機能はホスト (exe)を参照できないため。ホスト側の値 (アプリの名前・データ・アイコンの置き場所・作ったページの参照)は、SDK の `AppEnvironment`・`IPageCache`・`IFeatureStatus` を DI から受け取る
- 設定ページ・DEBUG ページは、全機能の設定を並べる側・開発用なので、ホストに残す (`MmmTool/Features/`)
- サイドバー・トレイ・起動時の準備の並びは、ホストが `AddFeaturePlugin` を呼ぶ順で決まる
- 決定 (ユーザーの決定): 機能の選択はビルド時 (参照の追加・削除)で行う。配布物の `Plugins` フォルダーに DLL を置くだけで入る形 (実行時の読み込み)は作らない
- 実行時の読み込みをユーザーが求めたときの設計上の条件: 実行時に読み込んだ DLL の XAML のリソース (`.pri`)の解決方法を、公式の情報で確かめる (確かめるまでは、画面をコードで組む・配置時に `.pri` をマージする・`XamlReader` で読む、のどれにするかを決めない)。動作中の DLL の入れ替えはできないので、追加・削除は再起動で反映する。WinAppSDK・CommunityToolkit・MmmSdk は、ホストと同じ版を 1 つだけ読み込む。サイドバーとトレイの並びは、登録順ではなく、明示の順序の値にする
- 最終形にどう近づくか: 機能とホストの境界 (入口の型)を SDK に決めたので、機能の追加・削除はホストの参照と 1 行で済む。境界が決まっているので、ローダーを足すことになっても、機能側の作り直しは要らない
- 例外: DEBUG ページ (ホストの `Features/Debugging`)はリマインダーのデータを表示するので、リマインダーのライブラリを参照する。リマインダーを外すときは、DEBUG ページも直す

### DB のコードは、機能とは別の `MmmTool.Data` にまとめる
- 決定 (ユーザーの決定。意図した仕様で、レビューで指摘・提案しない): リレーショナル DB への保存のコードを、機能のライブラリ (`MmmTool.<機能>.Core`)には置かず、`MmmTool.Data`(DB の種類に依存しない部分)と `MmmTool.Data.<種類>`(DB 別の実装。最初は `MmmTool.Data.SqlServer`)にまとめる。全機能の表・SQL を、この 2 種類のプロジェクトに集める
  - `MmmTool.Data`: DB の行のクラス (表の列と 1 対 1。`ReminderRow` など。DB の形のまま)と、アプリの型 (`Reminder` など)との変換
  - `MmmTool.Data.<種類>`: その DB の Repository の実装 (`SqlServerReminderRepository` など)。DB 別のドライバーは SDK の `MmmSdk.Db.<種類>` を参照する
  - 参照の向き: `MmmTool.Data.<種類> → MmmTool.Data → MmmTool.<機能>.Core → MmmSdk.Core`。機能のライブラリは `MmmTool.Data` を参照しない。保存先は、ホスト (`MmmTool`)が設定 (`Database.Mode`)で選び、DB のときだけ、機能が登録した JSON の Repository を、`MmmTool.Data.<種類>` の登録 (`AddSqlServerData()`)で置き換える
- 理由
  - プロジェクトの数を、DB の種類の数に抑える (DB を使う機能の数に比例させない)。機能ごとに `MmmTool.<機能>.Data.<種類>` を持たせると、「DB を使う機能 × DB の種類」だけ増える
  - DB のことを 1 か所で一括管理する (表の行のクラスと SQL は `MmmTool.Data*`、作成用スクリプトは `Sql/`、設計は `docs/specs/database.md`)
  - DB の行の形 (日付なしは `9999-12-31`・時刻は `time`・NULL なしなど)と、アプリの型 (`Reminder`。日付なしは `NoDate`・時刻は `HHmm`)は別。行のクラスを分けて、変換を 1 か所に集める
- 代償 (意図したもの。指摘しない): `MmmTool.Data` が、DB に保存する機能の Core を参照する。その機能を配布物から外すときは、`MmmTool.Data*` の対応する部分も外す。機能どうしの独立性は、保たれる (機能の Core は `MmmTool.Data` を参照しない)。アプリの型と DB の行の型が別なので、同じような型が 2 つある
- エンティティ (アプリの型。`Reminder` など)は、機能の Core に置き、`MmmTool.Data` には集めない (ユーザーの決定。意図した仕様)。参照の向きを、中心 (アプリの型)から外側 (DB)に向けない。エンティティを `Data` に移すと、機能の Core が全機能のエンティティと行のクラスを持つプロジェクトを参照することになり、エンティティと、それを扱う処理 (`ReminderDates` など)が別のプロジェクトに分かれる。機能をまたいで共有するエンティティ (`AppUser`)の置き場所は、`AppUser` を扱うときに決める
  - ユーザーの本来の希望は、各機能のエンティティも `MmmTool.Data` に集めて、DB まわりを完全に一括管理すること。使わない機能のエンティティが入ることも承知のうえで、上の理由 (参照の向き・エンティティと処理が分かれる)を考えて、見送った (ユーザーの判断)
  - 再び検討するのは、機能をまたいで共有するエンティティが `AppUser` のほかにも出て、機能の Core に置けなくなったとき
- 最終形にどう近づくか: DB の種類を足すときは、`MmmTool.Data.<種類>` を 1 つ足すだけで、機能の Core は変わらない。DB の情報は、2 種類のプロジェクトと `Sql/` を見れば分かる

### 起動時の準備 (`IStartupTask`)
- 起動時に、CLI補助の利用状態の読み込み・リマインダー監視の開始・リンクの先読みなどを行いたい。これらは UI スレッド (`DispatcherQueue`)で動く必要がある
- `IHostedService` にしない理由: Generic Host の `IHostedService` は、Host が内部で `ConfigureAwait(false)` を使うため、UI スレッドで動く保証がない。そのため、`App.OnLaunched` で `TrayIcon` を解決したあと・`MainWindow` を作る前に、UI スレッドで登録順に `await` する
- 起動時の準備で起動を止めたくない例外 (読み込みの失敗など)は、`<機能>Startup` の中で受け止め、画面を開いたときに知らせる

### 共有部品を別リポジトリの SDK に分ける
- JSON の保存・設定ストア・ウィンドウ位置の保存・パスを開く処理・通知ダイアログは、このアプリ以外でも使える汎用の部品。別リポジトリ `MmmSdk` に分け、Git サブモジュールとして取り込み、プロジェクト参照でつなぐ。アプリ固有の Entity・Repository は、アプリの Core に残す
- 複数のアプリで同じ部品を使い回せる。SDK はアプリを知らない (参照の向きは一方向)
- 影響: 共通のパッケージ (Windows App SDK など)のバージョンは、各リポジトリに 1 か所ずつ書くので、上げるときは SDK を先にする。SDK を直したら、SDK が先にコミット・push する

### テストプロジェクトを作らない
- テストプロジェクトは、ユーザーの決定で、ユーザーが「作る」と言うまで作らない (画面を目で見て確認する運用で進めているため)。レビューや作業のまとめで、テストが無いことを指摘・提案しない
- 作ると決めたら、Core の処理 (リマインダーの判定・日付の変換・壊れた JSON の退避・クリップボード転送の変換など)から作る
