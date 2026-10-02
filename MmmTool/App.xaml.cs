using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MmmTool.Core.Repositories;
using MmmTool.Core.Repositories.Json;
using MmmTool.Core.Services;
using MmmTool.Interop;
using MmmTool.Services;
using MmmTool.Services.Terminal;
using MmmTool.ViewModels;
using MmmTool.Views;
using MmmTool.Views.Dialogs;

namespace MmmTool;

public partial class App : Application
{
    private readonly SingleInstanceGuard _instanceGuard = new();
    private readonly IHost _host;
    private Window? _window;

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

        // Services
        services.AddSingleton<CliSettingsService>();
        // 終了時（Host の破棄時）に添付の一時フォルダを削除する
        services.AddSingleton<AttachmentStore>();
        // セッションは利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IImageConverter, ImageConverter>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<CliAssistPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<WorkingDirectoryDialog>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CliAssistViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<WorkingDirectoryDialogViewModel>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        // 画面を作る前に読み込む（前回の作業ディレクトリでシェルを始めるため）。失敗は画面側で通知する
        await _host.Services.GetRequiredService<CliSettingsService>().LoadAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Closed += async (_, _) =>
        {
            await _host.StopAsync();
            _host.Dispose();
        };
        _window.Activate();
    }
}
