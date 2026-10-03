---
name: winui3-mvvm
description: C# + WinUI 3 (Windows App SDK) デスクトップアプリの開発ルール。WinUI 3 / XAML / MVVM / CommunityToolkit.Mvvm のプロジェクトを新規作成・設定・実装・修正するときに使う。
---

# C# + WinUI 3 プロジェクトのお約束

最終形を基準に設計する（共通ルール参照）。土台は最初から以下の形で作る。

## ソリューション構成
プロジェクトは依存の向き（UI あり / なし）で分け、プロジェクトの中は機能ごとのフォルダ（Vertical Slice）にする。層ごとのフォルダ（`Views/` `ViewModels/` `Entities/` 等）は作らない。名前空間はフォルダどおり。
- `<App>.Core`（`net10.0` クラスライブラリ。Windows / WinUI を参照しない）
  - `<機能>/` … その機能の Entity（保存データ。保存先に依存しない普通のクラス）・Repository のインターフェース・UI 非依存の処理を平置き
  - `<機能>/Json/` … 保存先ごとの実装（例：`Json<名前>Repository` と、その機能のソース生成 Context `<機能>JsonContext`）。保存先ごとにサブフォルダを分ける
- `<App>`（WinUI 3 アプリ）… `Core` を参照する
  - `Features/<機能>/` … その機能の View・ViewModel・行などの表示用の型・UI / Windows 依存のサービス・Converter・DI 登録（`Add<機能>()`）を平置き。大きい部品はサブフォルダに分けてよい（例：`Features/CliAssist/Terminal/`）
  - `Shell/` … 画面の枠（メインウィンドウ・ナビゲーション・タスクトレイ等、機能を載せる側）
  - `Services/` … 機能をまたいで使う UI サービス（ダイアログ・ピッカー等）。機能固有のものは置かない
  - `Controls/` … 機能をまたいで使う汎用のコントロール
  - `Interop/` … Win32 P/Invoke。`NativeMethods` に internal static partial で集約し、`LibraryImport` で書く。大きくなったら用途ごとの partial ファイル（`NativeMethods.<用途>.cs`）に分ける
- 共通部分（`Shell/` `Services/` `Controls/`）は特定の機能を参照しない。機能から共通部分への一方向にする（機能固有の画面を開く口は、その機能に置く）
- XAML で同じ名前空間の型は `local:` で参照する
- 名前空間がよく使う型名を隠さないよう、フォルダ名を選ぶ（例：`Debug` は `System.Diagnostics.Debug` を隠すので `Debugging`）
- 外部ライブラリ依存の大きい保存先（EF Core, AWS SDK 等）は、採用時に `<App>.Data.<種類>` プロジェクトへ分け、Core に持ち込まない
- 「Common」のような用途の曖昧な共通プロジェクトは作らない
- 複数のアプリで共有する部品のライブラリ（SDK）も同じ考え方で、機能の代わりに提供する部品ごとのフォルダにする（例：`<Sdk>.Core/Settings/`・`<Sdk>.WinUI/Notifications/`）。部品の View・ViewModel・サービスは同じフォルダに平置き。DI 登録はプロジェクトごとに 1 つ（`Add<Sdk>Core()` 等）

## Entities / 保存
- Entities は ID を必ず持つ（アプリ側で採番。`Guid` 等）。DB 固有の属性（`[Key]` `[Table]` 等）を付けない
- Entity 同士はオブジェクト参照ではなく ID で参照する（JSON / NoSQL でも成り立つように）
- Repository のインターフェースは非同期（`Task`）にする
- ローカル保存の既定は JSON（`System.Text.Json`、日本語は非エスケープ、インデントあり）。書き込みは一時ファイル経由で置き換えて破損を防ぐ
- JSON はソース生成（`JsonSerializerContext`）で読み書きする。Context は機能ごとに分け、シリアライザの設定は 1 か所（共有部品のライブラリがあればそこ）にまとめて各 Context で使う

## DI / 起動
- Generic Host（`Microsoft.Extensions.Hosting`）で DI・ログ・設定・常駐処理を扱う
- Service / Repository / ViewModel は Host に登録し、コンストラクタ注入で受け取る。`new` で直接作らない（Entity などのデータは除く）
- DI の登録は機能ごとの拡張メソッド（`Features/<機能>/<機能>ServiceCollectionExtensions.cs` の `Add<機能>()`）に書き、`App` はそれを呼ぶだけにする。機能を足すときに触るのが、その機能のフォルダと `App` の 1 行で済むように
- ナビゲーションのページ・トレイメニューの項目・起動時の準備などは、機能側から登録する（`App` や `MainWindow` に機能の一覧を持たない）
- 保存先の切り替えは、その機能の登録箇所（`Add<機能>()` の Repository の行）だけで行う
- UI スレッドで行う起動時の処理は、Generic Host の `IHostedService` にしない（Host は内部で `ConfigureAwait(false)` を使うため、UI スレッドで動く保証がない）。アプリ側で起動時の準備の口を用意し、UI スレッドから順に呼ぶ

## 構成の型（具体例）
新しいプロジェクトも、この形・名前でそのまま作る。

### フォルダと名前
```
<App>.Core/
  <機能>/                 Entity・I<名前>Repository・<名前>Service（UI 非依存）
    Json/                 Json<名前>Repository・<機能>JsonContext
<App>/
  App.xaml(.cs)           Host の構築・ConfigureServices・起動と終了の順序だけ
  Shell/                  MainWindow・MainViewModel・NavigationItem・NavigationPage・NavigationArea
                          PageProvider・IStartupTask・SingleInstanceGuard・ShellServiceCollectionExtensions
    Tray/                 TrayIcon・ITrayMenuSource・TrayMenuItem（トレイ常駐のとき）
  Services/               IDialogService・DialogService・ピッカー（機能をまたぐものだけ）
  Controls/               機能をまたぐ汎用コントロール
  Interop/                NativeMethods.cs（宣言と用途の一覧）・NativeMethods.<用途>.cs
  Features/<機能>/        <名前>Page / <名前>Window（View）・<名前>ViewModel・<名前>Item（行）
                          I<機能>DialogService・<機能>TrayMenuSource・<機能>Startup
                          <機能>ServiceCollectionExtensions
```
- MVVM の役割はクラス名で分かるようにする（View = `*Page` / `*Window`、ViewModel = `*ViewModel`、Model = Core の Entity・Service）。フォルダは役割ではなく機能で分ける
- 機能のフォルダ名は機能の名前（例：`Reminders` / `Links` / `CliAssist`）。中の型名は扱う物の単数形（`Reminder` / `ReminderService`）

### 機能の DI 登録
```csharp
public static class RemindersServiceCollectionExtensions
{
    public static IServiceCollection AddReminders(this IServiceCollection services)
    {
        // 保存先（DB に替えるときはこの行だけを差し替える）
        services.AddSingleton<IReminderRepository, JsonReminderRepository>();

        services.AddSingleton<ReminderService>();
        services.AddSingleton<IReminderDialogService, ReminderDialogService>();
        services.AddSingleton<ITrayMenuSource, ReminderTrayMenuSource>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<ReminderInputWindow>();
        services.AddTransient<ReminderInputViewModel>();

        // サイドバーにページがある機能だけ
        services.AddNavigationPage<ReminderPage>("リマインダー", "", NavigationArea.Top);
        services.AddStartupTask<ReminderStartup>();
        return services;
    }
}
```
- `App.ConfigureServices` は「外部の SDK → 共通の UI サービス → `AddShell()` → 各機能の `Add<機能>()`」を呼ぶだけ。機能を登録した順が、サイドバー・トレイメニュー・起動時の準備の並び順になる
- ページは Transient で登録し、`PageProvider` が初回に DI から作ってキャッシュする（ナビゲーションのたびに作り直さない）。項目のキーはページの型名
- `AddNavigationPage` / `AddStartupTask` は `ShellServiceCollectionExtensions` に置き、機能側から呼ぶ
- DEBUG 専用の機能は、その機能の `Add<機能>()` の中を `#if DEBUG` で囲む（`App` 側で分岐しない）

### 起動時の準備
```csharp
public interface IStartupTask
{
    Task StartAsync();
}
```
- `App.OnLaunched` で `Host.StartAsync()` のあと、メインウィンドウを作る前に、UI スレッドで登録順に `await` する
- 読み込みの失敗など、起動を止めたくない例外は `<機能>Startup` の中で受け止め、画面を開いたときに知らせる

### ダイアログ
- 共通の `IDialogService` は機能に依存しない汎用のもの（確認ダイアログ等）だけにする
- 機能固有の画面を開く口は、その機能の `I<機能>DialogService`（例：`ShowInputAsync` / `ShowListAsync`）に置く。親ウィンドウの決定・モーダルの管理は共通の `DialogService` の部品（`Owner` / `ShowModalAsync`）を使う
- ピッカーなど親ウィンドウが要るサービスも、親は共通の `DialogService.Owner` から取る

### JSON のソース生成
```csharp
// <Sdk>.Core/Storage/ReadableJsonOptions.cs（設定はここ 1 か所）
public static class ReadableJsonOptions
{
    public static JsonSerializerOptions Create() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

// <App>.Core/Reminders/Json/ReminderJsonContext.cs（機能ごと）
[JsonSerializable(typeof(ReminderFile))]
internal sealed partial class ReminderJsonContext : JsonSerializerContext
{
    public static ReminderJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
```
- 設定のインスタンスは 1 つの Context に結び付くので、Context ごとに `Create()` で新しく作る（共有の static インスタンスを使い回さない）
- Context は `internal`。型の登録はその機能の型だけにする

### 移動・改名
- ファイルの移動は `git mv` で行い、履歴を残す。名前空間・XAML の `x:Class`・`<see cref>` もあわせて直す

## C# の書き方
- コンストラクタ：DI などで受け取って保持するだけなら、プライマリコンストラクタ（`class Foo(Bar bar)`）にする。コンストラクタの中で初期化の処理（イベント購読・タイマー作成・`InitializeComponent` 等）があるなら、従来の形（`private readonly` のフィールド＋コンストラクタ）にする
- 排他のロックは `System.Threading.Lock`（`private readonly Lock _gate = new();`）。`object` をロックに使わない。非同期の中で待つ排他は `SemaphoreSlim`
- XAML の値の変換は、`IValueConverter` ではなく `x:Bind` の関数呼び出し（`{x:Bind local:ThumbnailImage.FromFile(Path)}`）を使う
- 後始末（`Dispose`）は、DI コンテナから破棄されるものは `IDisposable` のままにする（`IAsyncDisposable` にすると、DI コンテナが `ConfigureAwait(false)` で待つので、後から破棄されるものの後始末が UI スレッドの外で動く）

## View / ViewModel の責務
- MVVM には CommunityToolkit.Mvvm を使う（`ObservableObject` / `[ObservableProperty]` / `[RelayCommand]`）
- ViewModel はアプリ側に置き、UI 型（Window, Page, Control 等）を参照しない
- コードビハインドは UI 固有の処理（ウィンドウ操作、ピッカー、フォーカス、ページ遷移等）に限る。業務ロジックや状態は ViewModel に置く
- View は `{x:Bind}` でバインドする（必要な箇所のみ `Mode=TwoWay`）
- ファイル選択やダイアログなど UI が必要な処理は、インターフェース付きの Service 経由で ViewModel から呼ぶ

## コメント
- 変数・フィールド・プロパティ・メソッドなど宣言の上のコメントは、`//` の行コメントではなく XML ドキュメントコメント（`///` の `<summary>` 形式）で書く
- `<summary>` は簡潔に（名前を和訳する程度の短さ）。長い説明・背景・理由・注意点は `<remarks>` に書く
- 書く対象：基本はすべての宣言に `<summary>` を付ける。
  - public / internal の型・メソッド（コンストラクタ含む）・プロパティ・フィールド・イベント・定数・enum の値は、必ず書く（例外なし）
  - private / protected は必要に応じて書く。ただし基本は書く方向で、名前だけで意味が明らかなもの（単純な `_logger` など）だけ省略してよい
  - インターフェースの実装・override は、インターフェース側に書いてあれば `/// <inheritdoc />` でよい
  - 自動生成ファイル（`obj/` `bin/` `*.g.cs`）は対象外
- メソッド・コンストラクタ・デリゲートは、引数と戻り値も漏らさず書く（例外なし。自明な引数でも省略しない）
  - Visual Studio で `///` を打ったときに自動生成される要素を、その順番のままベースにする：`<summary>` → `<typeparam>`（ジェネリックのとき・型パラメータの宣言順）→ `<param>`（引数の宣言順）→ `<returns>`（戻り値が `void` 以外のとき。`Task` は「〜の完了を表すタスク」、`Task<T>` は中身の T の意味を書く。VS の自動生成と同じく `Task` にも付ける）
  - そのあとに、手で足す要素を置く：`<remarks>` → `<exception>` → `<see>` などを含むその他
  - 型（class / record 等）のプライマリコンストラクターの引数も同じ順序で、型の `<summary>` の次に `<param>` を書く（`<remarks>` より前）
  - 空のタグを残さない（`<param name="x"></param>` のままにしない）。`<param>` / `<returns>` も短く、長い説明は `<remarks>` へ
  - `<inheritdoc />` を使う場合は上記の対象外（インターフェース側に書く）

## プロジェクト設定
- アンパッケージ。配布サイズを小さくするため、既定はフレームワーク依存（実行する PC に .NET Desktop Runtime と Windows App Runtime が必要。ランタイムを入れてもらえない配布先のときだけセルフコンテインにする）
  - `.csproj`: `<WindowsPackageType>None</WindowsPackageType>` / `<SelfContained>false</SelfContained>` / `<WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>`
  - フレームワーク依存ではトリミング（`PublishTrimmed`）は使えない。それでもトリミングに戻せるよう、JSON はソース生成のまま書く
  - `launchSettings.json` は `"commandName": "Project"` のプロファイルのみにする
  - `Package.appxmanifest` は動作確認が取れるまで削除しない
- .NET は最新安定版、`Nullable` / `ImplicitUsings` 有効
- ビルドの共通設定は、リポジトリの根元に 1 組だけ置く（csproj に同じ設定を書き並べない）
  - `Directory.Build.props` … 全プロジェクト共通の設定（`Nullable` / `ImplicitUsings` / `GenerateDocumentationFile`（XML コメントの検査。IDE0005 の検査にも必要）/ `EnforceCodeStyleInBuild`）。アプリ側は `PublishDocumentationFile` / `PublishReferencesDocumentationFiles` を false にして、XML ファイルを配布物に含めない。対象フレームワーク・WinUI・`Platforms` など、プロジェクトごとに違うものは csproj に書く
  - `Directory.Packages.props` … 中央パッケージ管理（`ManagePackageVersionsCentrally`）。バージョンはここだけに書き、csproj の `PackageReference` には書かない
  - `.editorconfig`（`root = true`）… コードスタイル。守らせたいもの（未使用 using・ファイル単位の名前空間・using の位置・複数行の本体の波かっこ）は `warning` にしてビルドで検査する。好みの範囲のもの（`var`・コレクション式・プライマリコンストラクタ・名前の付け方）は `suggestion`。`charset` は書かない（BOM が要るファイルがあるため）
  - 3 つともソリューションの「Solution Items」フォルダに入れて、VS から見えるようにする
  - サブモジュールで取り込む SDK は、SDK のリポジトリの根元に自分の 1 組を持つ。MSBuild と .editorconfig は近いほうを使うので、アプリと SDK の設定は混ざらない（SDK はアプリを知らないまま）。両方で使うパッケージのバージョンは各リポジトリに 1 か所ずつになるので、上げるときは SDK を先に上げてアプリを同じバージョンにする
- 構成は x64 のみ（`Platforms` = x64、`RuntimeIdentifiers` = win-x64）
- 多重起動の扱いはプロジェクトごとに決める（プロジェクトの CLAUDE.md / 仕様に従う）。禁止する場合は、EXE パスのハッシュで Mutex 名を作り、同一 EXE の二重起動だけを防ぐ（Debug / Release など別パスの EXE は同時起動できる）
- 配布が必要な場合は `dotnet publish -c Release -r win-x64` の出力フォルダをコピーする
