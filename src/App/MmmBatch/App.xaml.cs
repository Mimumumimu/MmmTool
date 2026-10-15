using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MmmBatch.Data.SqlServer;
using MmmBatch.MessageSending;
using MmmBatch.ReminderSending;
using MmmBatch.Sending;
using MmmBatch.Sending.Core;
using MmmBatch.Shell;
using MmmBatch.Shell.Main;
using MmmBatch.Shell.Settings;
using MmmSdk.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.Logging;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.SingleInstance;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmSdk.WinUI.Utilities;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch;

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

    /// <summary>終了の処理を始めたか</summary>
    private bool _exiting;

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
    private static void ConfigureServices(IServiceCollection services, FatalErrorHandler fatalErrors)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(fatalErrors);

        // SDK (JsonFileStore・設定ストア・位置保存・通知ダイアログ・確認ダイアログなど)
        services.AddMmmSdkCore(AppInfo.DataDirectory);
        services.AddMmmSdkWinUI();
        // アプリの名前・データのフォルダー・アイコンの置き場所 (SDK の画面が、アイコンを読むために受け取る)
        services.AddSingleton(new AppEnvironment(AppInfo.Name, AppInfo.DataDirectory, AppIcon.FilePath));

        // タスクトレイ (メニューは「終了」だけ。クリックでウィンドウを開く)
        services.AddMmmSdkTray(new TrayIconOptions(
            ToolTip: AppInfo.Name,
            WindowClassName: AppInfo.TrayWindowClassName,
            ExitText: "終了",
            IconPath: AppIcon.FilePath));

        // 機能のオン・オフの管理と、サイドバー (SDK)。MmmBatch の機能は、オフにできない (オン・オフの画面は持たない)
        services.AddFeatureService();
        // 共通の設定ファイルを、サイドバーの並びを読む前に先読みしておく (最初の読み込みで、UI スレッドが止まらないように)
        services.AddStartupTask<SettingsStoreStartup>();
        services.AddNavigationHost();

        // 機能に共通の送信の処理 (送る口・送信のログ・毎分の監視・通知の窓口)と、その DB (SQL Server)
        services.AddSendingCore(AppInfo.LogDirectory);
        services.AddMmmBatchSqlServer();

        // DB の接続の設定画面。閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddSingleton<ISendingDialogService, ConnectionSettingsDialogService>();
        services.AddTransient<DatabaseConnectionViewModel>();
        services.AddTransient<ConnectionSettingsViewModel>();
        services.AddTransient<ConnectionSettingsWindow>();

        // 機能 (バッチ処理)。足すときは、ここに 1 行足すだけ (サイドバーの項目・毎分の仕事・通知は、機能が自分で登録する)
        services.AddFeaturePlugin<ReminderSendingPlugin>(order: 1);
        services.AddFeaturePlugin<MessageSendingPlugin>(order: 2);

        services.AddSingleton<MainWindow>();
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
    /// 基本は、トレイだけで動き続ける (ウィンドウは出さない)。最初の実行で、どれかの仕事が状況を読めなかったとき (接続の設定が無い・つながらない)だけ、気づけるよう、ウィンドウを出す。
    /// このアプリは、個々の機能の名前を持たない (機能は <c>AddFeaturePlugin</c> で登録し、毎分の仕事・通知・サイドバーの項目は、機能が自分で登録する)。
    /// </remarks>
    private async Task StartAsync()
    {
        await _host.StartAsync();

        // トレイは画面より先に作る。Host は作った順の逆に破棄するので、終了時に画面・送信の後始末が済んでからトレイアイコンが消える
        var tray = _host.Services.GetRequiredService<TrayIcon>();

        // 起動時の準備 (設定ファイルの先読みなど)。画面を作る前に行う
        await _host.Services.GetRequiredService<FeatureService>().StartAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        // 接続の設定の画面などを、メインウィンドウの上に出すため
        _host.Services.GetRequiredService<IDialogHost>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.BringToFront();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        tray.Show();

        // 送信の失敗・DB の失敗は、ウィンドウを開かなくても気づけるよう、トレイの通知で知らせる (通知は UI スレッドで出す)
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _host.Services.GetRequiredService<BatchAlertService>().AlertRaised
            += (_, message) => dispatcher.TryEnqueue(() => tray.ShowNotification(AppInfo.Name, message, isError: true));

        // 毎分の仕事を始める (起動の直後にも 1 回動く)。Host の破棄で止まる。つながらないときは、画面の InfoBar で知らせる (起動は止めない)
        var monitor = _host.Services.GetRequiredService<BatchMonitor>();
        monitor.Start();
        await monitor.FirstRunCompleted;
        if (monitor.HasError)
        {
            _window.BringToFront();
        }
    }

    /// <summary>アプリを完全に終了する (トレイメニューの「終了」から)</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>送信の後始末 → トレイアイコンの解放 → アプリの終了 の順に行う。後の 2 つは Host の破棄 (作った順の逆)で行われる。</remarks>
    private async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _window?.PrepareExit();
        // 開いているウィンドウ (接続の設定など)は、Host が生きているうちに閉じる (メインウィンドウは Exit が閉じる)
        _host.Services.GetRequiredService<IDialogHost>().CloseAll(_window);
        await _host.StopAsync();
        _host.Dispose();
        Exit();
    }
}
