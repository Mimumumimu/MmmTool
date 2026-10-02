using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
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

public partial class App : Application
{
    private readonly SingleInstanceGuard _instanceGuard = new();
    private readonly IHost _host;
    private MainWindow? _window;

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

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Repositories（保存先を替えるときはここだけを差し替える）
        services.AddSingleton(new JsonFileStore(Path.Combine(AppContext.BaseDirectory, "Data")));
        services.AddSingleton<ICliCommandRepository, JsonCliCommandRepository>();
        services.AddSingleton<ICliSettingsRepository, JsonCliSettingsRepository>();
        services.AddSingleton<ILinkRepository, JsonLinkRepository>();

        // Services
        services.AddSingleton<CliSettingsService>();
        // 最後に読み込み・保存した構成を、編集ページとトレイのリンクメニューで共有するため、アプリ全体で 1 つ
        services.AddSingleton<LinkMenuService>();
        services.AddSingleton<LinkOpener>();
        // 終了時（Host の破棄時）に添付の一時フォルダを削除する
        services.AddSingleton<AttachmentStore>();
        // セッションは利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IImageConverter, ImageConverter>();

        // トレイ（メニューの項目は、ここに登録した順に区切り線で分けて並ぶ）
        services.AddSingleton<TrayIcon>();
        services.AddSingleton<ITrayMenuSource, LinkTrayMenuSource>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<CliAssistPage>();
        services.AddTransient<LinkEditorPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<WorkingDirectoryDialog>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CliAssistViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LinkEditorViewModel>();
        services.AddTransient<WorkingDirectoryDialogViewModel>();
    }

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

        tray.OpenRequested += (_, _) => _window.ShowAndActivate();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        // 起動時はトレイだけ。ウィンドウはトレイから開いたときに初めて出す
        tray.Show();
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
