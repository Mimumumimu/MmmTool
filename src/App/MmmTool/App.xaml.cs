using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MmmSdk.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Logging;
using MmmSdk.Core.Components.SingleInstance;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Tray;
using MmmSdk.WinUI.Utilities;
using MmmTool.Backlog;
using MmmTool.CliAssist;
using MmmTool.ClipboardTransfer;
using MmmTool.Data.SqlServer;
using MmmTool.Features.Database;
using MmmTool.Features.Database.Choice;
using MmmTool.Features.Debugging;
using MmmTool.Features.Settings;
using MmmTool.Features.Users;
using MmmTool.Links;
using MmmTool.Reminders;
using MmmTool.Shell;
using MmmTool.Shell.Main;

namespace MmmTool;

/// <summary>アプリケーション (起動・終了と DI の構成)</summary>
public partial class App : Application
{
    /// <summary>多重起動の防止</summary>
    private readonly SingleInstanceGuard _instanceGuard = new(AppInfo.Name);
    /// <summary>復旧できないエラーの報告先 (ログ・ダイアログ・終了)</summary>
    private readonly FatalErrorHandler _fatalErrors;
    /// <summary>DI とライフタイムを扱う Host</summary>
    private readonly IHost _host;
    /// <summary>メインウィンドウ。まだ作っていなければ null</summary>
    private MainWindow? _window;

    /// <summary>多重起動を確認して、DI の構成を作る</summary>
    /// <remarks>未処理の例外の受け皿は、Host を作る前の失敗も受けられるよう、最初に付ける。</remarks>
    public App()
    {
        if (!_instanceGuard.IsFirstInstance)
        {
            NativeMessageBox.ShowInformation($"{AppInfo.Name} はすでに起動しています。", AppInfo.Name);
            Environment.Exit(0);
        }

        _fatalErrors = new FatalErrorHandler(new ErrorLog(AppInfo.LogDirectory), AppInfo.Name);
        _fatalErrors.AttachTo(this);

        InitializeComponent();

        // 設定 (appsettings.json・環境変数)とログの既定は使わないので、無効にする (起動時に読まないため)
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ContentRootPath = AppContext.BaseDirectory,
        });
        ConfigureServices(builder.Services, _fatalErrors);
        _host = builder.Build();
    }

    /// <summary>DI に登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="fatalErrors">復旧できないエラーの報告先 (トレイなど、SDK の部品が使う)</param>
    /// <remarks>機能ごとの中身 (保存先・サービス・画面)は、各機能の Add～ メソッドにある。</remarks>
    private static void ConfigureServices(IServiceCollection services, FatalErrorHandler fatalErrors)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(fatalErrors);

        // SDK (JsonFileStore・設定ストア・位置保存・パスを開く処理・通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択)。JsonFileStore はアプリ固有の保存でも共有する
        services.AddMmmSdkCore(AppInfo.DataDirectory);
        services.AddMmmSdkWinUI();

        // 画面の枠 (メインウィンドウ・サイドバー・トレイ)
        services.AddShell();

        // 設定ページの先頭の部品 (全般・機能の一覧)。設定ページの部品は並び順の値で並ぶので、呼ぶ位置は問わない
        services.AddSettingsSections();

        // ログイン (DB モード)。機能の起動時の準備より先に動かすため、機能の登録より前に呼ぶ
        services.AddUserSignIn();

        // 機能。起動時の準備は、登録した順に実行する。サイドバー・トレイメニュー・設定の機能の一覧・設定の部品は、order の順に並ぶ (登録の順は問わない)
        // order の並び: よく使う順 (CLI補助が主な機能)。トレイは、メニューが下から開くので、下 (大きい値)ほどカーソルに近い。リンクを下にして近くに置く
        services.AddFeaturePlugin<CliAssistPlugin>(order: 1);
        services.AddFeaturePlugin<ClipboardTransferPlugin>(order: 4);
        services.AddFeaturePlugin<RemindersPlugin>(order: 2);
        services.AddFeaturePlugin<BacklogPlugin>(order: 5);
        services.AddFeaturePlugin<LinksPlugin>(order: 3);

        // DB への保存 (SQL Server)。機能が登録した JSON の保存先を、設定が DB のときだけ置き換えるので、機能の登録のあとに呼ぶ
        services.AddSqlServerData();

        // 保存先の画面 (設定ページの部品と、初回の選択)
        services.AddDatabaseScreens();

        services.AddDebugging();
        services.AddSettings();
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await StartAsync();
        }
        catch (Exception ex)
        {
            // 起動が途中で止まると、ウィンドウもトレイも出ないまま動き続けて気づけない。復旧できないので、ログ・ダイアログ・終了にする
            _fatalErrors.Report("起動に失敗しました", ex);
        }
    }

    /// <summary>Host を始めて、トレイと各機能を用意する</summary>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>
    /// 読み込みの失敗など、起動を止めたくない例外は、各機能の起動時の準備 (<see cref="MmmSdk.Core.Components.Features.IStartupTask"/>)の中で受け止め、画面を開いたときに知らせる。
    /// ここまで届いた例外は、復旧できない失敗として <see cref="OnLaunched"/> が報告する。
    /// </remarks>
    private async Task StartAsync()
    {
        await _host.StartAsync();

        // トレイは画面・各機能より先に作る。Host は作った順の逆に破棄するので、終了時に各機能の後始末が済んでからトレイアイコンが消える
        var tray = _host.Services.GetRequiredService<TrayIcon>();

        // 保存先がまだ選ばれていない初回だけ、選択の画面を出す。各機能の準備より前に出し (保存先は準備で決まって使い始めるため)、選んだ内容を最初から効かせる
        // 選ばずに閉じたときは、先へ進ませずに終了する
        var databaseChoice = _host.Services.GetRequiredService<DatabaseChoiceService>();
        if (!await databaseChoice.ShowIfNeededAsync())
        {
            await ExitAsync();
            return;
        }

        // 各機能の起動時の準備 (設定の読み込み・時刻監視の開始など)。画面を作る前に行う (前回の作業ディレクトリでシェルを始めるため等)
        // オフにした機能の準備は行わない (オンにしたときに行う)
        await _host.Services.GetRequiredService<FeatureService>().StartAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        // メインウィンドウから開くダイアログを、メインウィンドウの上に出すため (リマインダーのメイン画面と使い分ける)
        _host.Services.GetRequiredService<IDialogHost>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.BringToFront();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        // 設定ページなど、画面からの終了の依頼も、トレイの「終了」と同じ処理にする
        _host.Services.GetRequiredService<AppExitService>().ExitRequested += async (_, _) => await ExitAsync();

        // 保存先の選択の画面は、最後のウィンドウを閉じてアプリごと終了しないよう、メインウィンドウを作るまで隠して残していた。ここで閉じる
        databaseChoice.CloseWindow();

        // DB モードで、ログインできていないとき、ログインの画面を出す (メインウィンドウを作ったあと。作る前に出すと、閉じたときにアプリごと終了しうる)
        // ログインを済ませないと先へ進ませない (トレイも出さない)。ログインせずに閉じたときは、終了する
        if (!await _host.Services.GetRequiredService<SignInPrompt>().ShowIfRequestedAsync())
        {
            await ExitAsync();
            return;
        }

        // 既定では、起動時はトレイだけ。ウィンドウはトレイから開いたときに初めて出す
        tray.Show();

        // メインウィンドウを出すのは、次のとき (ユーザーの登録が済んでから出す)
        // - 初回の起動 (保存先を選んだとき。トレイだけでは、何も起動していないように見えるため)
        // - 設定で「起動時にメイン画面を開く」がオンのとき (設定ストアは、機能の準備の前に先読み済み)
        if (databaseChoice.HasChosen || _host.Services.GetRequiredService<MainWindowSettingsService>().OpenOnStartup)
        {
            _window.BringToFront();
        }
    }

    /// <summary>アプリを完全に終了する (トレイメニューの「終了」から)。</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>
    /// 各機能の後始末 (ターミナルのシェル・添付の一時フォルダ等)→ トレイアイコンの解放 → アプリの終了 の順に行う。
    /// 後の 2 つは Host の破棄 (作った順の逆)で行われる。未保存の編集内容は確認せずに破棄する。
    /// </remarks>
    private async Task ExitAsync()
    {
        _window?.PrepareExit();
        // 開いているウィンドウ (モーダル・リマインダーのメイン画面など)は、Host が生きているうちに閉じる (閉じる処理が、破棄済みのサービスを触らないように。メインウィンドウは Exit が閉じる)
        _host.Services.GetRequiredService<IDialogHost>().CloseAll(_window);
        await _host.StopAsync();
        _host.Dispose();
        Exit();
    }
}
