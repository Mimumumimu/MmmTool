using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MmmSdk.Core;
using MmmSdk.Core.Repositories;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Services;
using MmmTool.Core.Repositories;
using MmmTool.Core.Repositories.Json;
using MmmTool.Core.Services;
using MmmTool.Interop;
using MmmTool.Services;
using MmmTool.Services.Terminal;
using MmmTool.Services.Tray;
using MmmTool.ViewModels;
using MmmTool.Views;
using MmmTool.Views.Dialogs;

namespace MmmTool;

/// <summary>アプリケーション（起動・終了と DI の構成）</summary>
public partial class App : Application
{
    /// <summary>多重起動の防止</summary>
    private readonly SingleInstanceGuard _instanceGuard = new();
    /// <summary>DI・ログなどを扱う Host</summary>
    private readonly IHost _host;
    /// <summary>メインウィンドウ。まだ作っていなければ null</summary>
    private MainWindow? _window;

    /// <summary>多重起動を確認して、DI の構成を作る</summary>
    public App()
    {
        if (!_instanceGuard.IsFirstInstance)
        {
            NativeMethods.ShowInformation("MmmTool はすでに起動しています。", "MmmTool");
            Environment.Exit(0);
        }

        InitializeComponent();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        ConfigureServices(builder.Services);
        _host = builder.Build();
    }

    /// <summary>DI に登録する</summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Repositories（保存先を替えるときはここだけを差し替える）
        // SDK（JsonFileStore・設定ストア・位置保存・リンクを開く処理）。JsonFileStore はアプリ固有の保存でも共有する
        services.AddMmmSdkCore(Path.Combine(AppContext.BaseDirectory, "Data"));
        services.AddSingleton<ICliCommandRepository, JsonCliCommandRepository>();
        services.AddSingleton<ICliSettingsRepository, JsonCliSettingsRepository>();
        services.AddSingleton<ILinkRepository, JsonLinkRepository>();
        services.AddSingleton<IReminderRepository, JsonReminderRepository>();

        // Services
        services.AddSingleton<CliSettingsService>();
        // 最後に読み込み・保存した構成を、編集ページとトレイのリンクメニューで共有するため、アプリ全体で 1 つ
        services.AddSingleton<LinkMenuService>();
        // リマインダーは各画面と時刻監視でキャッシュを共有するため、アプリ全体で 1 つ。監視は Host の破棄時に止まる
        services.AddSingleton<ReminderService>();
        services.AddSingleton<ReminderMonitor>();
        services.AddSingleton<ReminderSettingsService>();
        // 終了時（Host の破棄時）に添付の一時フォルダを削除する
        services.AddSingleton<AttachmentStore>();
        // セッションは利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());
        // リマインダーのメイン画面はアプリ内で 1 枚だけ（開いていれば前面に出す）
        services.AddSingleton<ReminderWindowService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IImageConverter, ImageConverter>();
        // 通知ダイアログ（SDK）。ウィンドウはアプリ内で 1 枚だけ（サービスが持つ）
        services.AddMmmSdkWinUI();

        // トレイ（メニューの項目は、ここに登録した順に区切り線で分けて並ぶ）
        services.AddSingleton<TrayIcon>();
        services.AddSingleton<ITrayMenuSource, ReminderTrayMenuSource>();
        services.AddSingleton<ITrayMenuSource, LinkTrayMenuSource>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<CliAssistPage>();
        services.AddTransient<LinkEditorPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<WorkingDirectoryDialog>();
        services.AddTransient<DebugPage>();
        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<ReminderInputWindow>();
        services.AddTransient<ReminderListWindow>();
        services.AddTransient<ReminderMainWindow>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CliAssistViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LinkEditorViewModel>();
        services.AddTransient<WorkingDirectoryDialogViewModel>();
        services.AddTransient<DebugViewModel>();
        services.AddTransient<ReminderInputViewModel>();
        services.AddTransient<ReminderListViewModel>();
        services.AddTransient<ReminderMainViewModel>();
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        // 画面を作る前に読み込む（前回の作業ディレクトリでシェルを始めるため）。失敗は画面側で通知する
        await _host.Services.GetRequiredService<CliSettingsService>().LoadAsync();

        // トレイのリンクメニュー用に読み込んでおく
        try
        {
            await _host.Services.GetRequiredService<LinkMenuService>().LoadAsync();
        }
        catch (DataFileException)
        {
            // 失敗はサービスに残り、トレイのメニューとリンク画面（開いたときに読み直す）で知らせる
        }

        // トレイは画面・各機能より先に作る。Host は作った順の逆に破棄するので、終了時に各機能の後始末が済んでからトレイアイコンが消える
        var tray = _host.Services.GetRequiredService<TrayIcon>();
        _window = _host.Services.GetRequiredService<MainWindow>();
        // メインウィンドウから開くダイアログを、メインウィンドウの上に出すため（リマインダーのメイン画面と使い分ける）
        _host.Services.GetRequiredService<DialogService>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.ShowAndActivate();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        // 起動時はトレイだけ。ウィンドウはトレイから開いたときに初めて出す
        tray.Show();

        StartReminderMonitor();
    }

    /// <summary>リマインダーの時刻監視を始める</summary>
    /// <remarks>
    /// 監視はタイマーのスレッドから通知を求めてくるので、UI スレッドに切り替えて通知ダイアログを出す。
    /// 通知の本文をクリックして閉じたら、リマインダーのメイン画面を開く（発動済みの未対応はスヌーズに進む）。
    /// </remarks>
    private void StartReminderMonitor()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        var notifications = _host.Services.GetRequiredService<INotificationDialogService>();
        var reminderWindows = _host.Services.GetRequiredService<ReminderWindowService>();
        _host.Services.GetRequiredService<ReminderMonitor>().Start(
            (title, items) => dispatcher.TryEnqueue(
                () => notifications.Show(title, items, onClicked: () => dispatcher.TryEnqueue(
                    // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                    DispatcherQueuePriority.Low, async () => await reminderWindows.ShowFromNotificationAsync()))));
    }

    /// <summary>アプリを完全に終了する（トレイメニューの「終了」から）。</summary>
    /// <remarks>
    /// 各機能の後始末（ターミナルのシェル・添付の一時フォルダ等）→ トレイアイコンの解放 → アプリの終了 の順に行う。
    /// 後の 2 つは Host の破棄（作った順の逆）で行われる。未保存の編集内容は確認せずに破棄する。
    /// </remarks>
    private async Task ExitAsync()
    {
        _window?.PrepareExit();
        await _host.StopAsync();
        _host.Dispose();
        Exit();
    }
}
