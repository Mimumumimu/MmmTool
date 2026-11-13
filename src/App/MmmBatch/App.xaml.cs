using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MmmBatch.Data.SqlServer;
using MmmBatch.MessageSending;
using MmmBatch.NoteSending;
using MmmBatch.ReminderSending;
using MmmBatch.Sending.Core;
using MmmBatch.Shell;
using MmmBatch.Shell.Main;
using MmmSdk.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.Settings;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Hosting;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmSdk.WinUI.Utilities;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch;

/// <summary>アプリケーション</summary>
public partial class App : Application
{
    /// <summary>起動と終了の骨格</summary>
    private readonly AppHost _appHost;

    /// <summary>メインウィンドウ</summary>
    /// <remarks>まだ作っていなければ null</remarks>
    private MainWindow? _window;

    /// <summary>多重起動を確認して DI の構成を作る</summary>
    public App()
    {
        _appHost = new AppHost(this, AppInfo.Name, AppInfo.LogDirectory);
        InitializeComponent();
        _appHost.Build(ConfigureServices);
    }

    /// <summary>DI に登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    private static void ConfigureServices(IServiceCollection services)
    {
        // SDK (JsonFileStore・設定ストア・位置保存・通知ダイアログ・確認ダイアログなど)
        services.AddMmmSdkCore(AppInfo.DataDirectory);
        services.AddMmmSdkWinUI();
        // アプリの名前・データのフォルダー・アイコンの置き場所 (SDK の画面が、アイコンを読むために受け取る)
        services.AddSingleton(new AppEnvironment(AppInfo.Name, AppInfo.DataDirectory, AppInfo.CacheDirectory, AppIcon.FilePath));

        // タスクトレイ。メニューは「終了」だけで、クリックでウィンドウを開く
        services.AddMmmSdkTray(new TrayIconOptions(
            ToolTip: AppInfo.Name,
            WindowClassName: AppInfo.TrayWindowClassName,
            ExitText: "終了",
            IconPath: AppIcon.FilePath));

        // 機能のオン・オフの管理と、SDK のサイドバー。MmmBatch の機能は、オフにできない。オン・オフの画面は持たない
        services.AddFeatureService();
        // 共通の設定ファイルを、サイドバーの並びを読む前に先読みしておく (最初の読み込みで、UI スレッドが止まらないように)
        services.AddStartupTask<SettingsStoreStartup>();
        services.AddNavigationHost();

        // 機能に共通の送信の処理 (送る口・送信のログ・毎分の監視・通知の窓口)と、その DB (SQL Server)
        services.AddSendingCore(AppInfo.LogDirectory);
        services.AddMmmBatchSqlServer();

        // DB の接続の設定画面。閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddConnectionSettingsDialog();

        // バッチ処理の機能。足すときは、ここに 1 行足すだけで、サイドバーの項目・毎分の仕事・通知は、機能が自分で登録する
        services.AddFeaturePlugin<ReminderSendingPlugin>(order: 1);
        services.AddFeaturePlugin<MessageSendingPlugin>(order: 2);
        services.AddFeaturePlugin<NoteSendingPlugin>(order: 3);

        services.AddSingleton<MainWindow>();
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _appHost.LaunchAsync(StartAsync);
    }

    /// <summary>Host を始めてトレイと各機能を用意する</summary>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>
    /// 基本はトレイだけで動き続ける。
    /// ウィンドウは出さない。
    /// 最初の実行で、どれかの仕事が状況を読めなかったとき (接続の設定が無い・つながらない)だけ、気づけるよう、ウィンドウを出す。
    /// このアプリは個々の機能の名前を持たない。
    /// 機能は <c>AddFeaturePlugin</c> で登録し、毎分の仕事・通知・サイドバーの項目は機能が自分で登録する。
    /// </remarks>
    private async Task StartAsync()
    {
        await _appHost.StartHostAsync();
        var services = _appHost.Services;

        // トレイは画面より先に作る。Host は作った順の逆に破棄するので、終了時に画面・送信の後始末が済んでからトレイアイコンが消える
        var tray = services.GetRequiredService<TrayIcon>();

        // 起動時の準備で、設定ファイルの先読みなど。画面を作る前に行う
        await services.GetRequiredService<FeatureService>().StartAsync();

        _window = services.GetRequiredService<MainWindow>();
        // 接続の設定の画面などを、メインウィンドウの上に出すため
        services.GetRequiredService<IDialogHost>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.BringToFront();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        tray.Show();

        // 送信の失敗・DB の失敗は、ウィンドウを開かなくても気づけるよう、トレイの通知で知らせる (通知は UI スレッドで出す)
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        services.GetRequiredService<BatchAlertService>().AlertRaised
            += (_, message) => dispatcher.TryEnqueue(() => tray.ShowNotification(AppInfo.Name, message, isError: true));

        // 毎分の仕事を始める。起動の直後にも 1 回動く。Host の破棄で止まる。つながらないときは、画面の InfoBar で知らせ、起動は止めない
        var monitor = services.GetRequiredService<BatchMonitor>();
        monitor.Start();
        await monitor.FirstRunCompleted;
        if (monitor.HasError)
        {
            _window.BringToFront();
        }
    }

    /// <summary>アプリを完全に終了する</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>
    /// 送信の後始末 → トレイアイコンの解放 → アプリの終了 の順に行う
    /// 後の 2 つは Host の破棄 (作った順の逆)で行われる
    /// 順序と二重の呼び出しの防止は <see cref="AppHost.ExitAsync"/> が持つ
    /// </remarks>
    private Task ExitAsync() => _appHost.ExitAsync(_window, () => _window?.PrepareExit());
}
