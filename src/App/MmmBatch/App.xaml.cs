using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MmmBatch.Data.SqlServer;
using MmmBatch.Sending;
using MmmBatch.Sending.Core;
using MmmBatch.Shell;
using MmmBatch.Shell.Main;
using MmmBatch.Shell.Settings;
using MmmSdk.Core;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.Logging;
using MmmSdk.Core.Components.SingleInstance;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
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

        // リマインダーの送信 (送る処理と、送信の状況の画面)と、その DB (SQL Server)
        services.AddSending();
        services.AddMmmBatchSqlServer();

        // DB の接続の設定画面。閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddSingleton<ISendingDialogService, ConnectionSettingsDialogService>();
        services.AddTransient<DatabaseConnectionViewModel>();
        services.AddTransient<ConnectionSettingsViewModel>();
        services.AddTransient<ConnectionSettingsWindow>();

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

    /// <summary>Host を始めて、トレイと送信を用意する</summary>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>
    /// 基本は、トレイだけで動き続ける (ウィンドウは出さない)。送信の状況を読めなかったとき (接続の設定が無い・つながらない)だけ、気づけるよう、ウィンドウを出す。
    /// </remarks>
    private async Task StartAsync()
    {
        await _host.StartAsync();

        // トレイは画面より先に作る。Host は作った順の逆に破棄するので、終了時に画面・送信の後始末が済んでからトレイアイコンが消える
        var tray = _host.Services.GetRequiredService<TrayIcon>();

        // 送信の状況を読んでから、画面を作る (空の一覧が一瞬見えないように)。つながらないときは、画面の InfoBar で知らせる (起動は止めない)
        var plan = _host.Services.GetRequiredService<SendPlanViewModel>();
        var history = _host.Services.GetRequiredService<SendHistoryViewModel>();
        await Task.WhenAll(plan.RefreshAsync(), history.RefreshAsync());

        _window = _host.Services.GetRequiredService<MainWindow>();
        // 接続の設定の画面などを、メインウィンドウの上に出すため
        _host.Services.GetRequiredService<IDialogHost>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.BringToFront();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        tray.Show();

        // 送信の失敗・DB の失敗は、ウィンドウを開かなくても気づけるよう、トレイの通知で知らせる (通知は UI スレッドで出す)
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _host.Services.GetRequiredService<ReminderSendService>().AlertRaised +=
            (_, message) => dispatcher.TryEnqueue(() => tray.ShowNotification(AppInfo.Name, message, isError: true));

        if (plan.Error.IsOpen || history.Error.IsOpen)
        {
            _window.BringToFront();
        }

        // 毎分の送信を始める。Host の破棄で止まる
        _host.Services.GetRequiredService<SendMonitor>().Start();
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
