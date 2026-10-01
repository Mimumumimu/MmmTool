using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MmmTool.Interop;
using MmmTool.Services;
using MmmTool.Services.Terminal;
using MmmTool.ViewModels;
using MmmTool.Views;

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
        // Services
        // セッションは利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<CliAssistPage>();
        services.AddTransient<SettingsPage>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CliAssistViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Closed += async (_, _) =>
        {
            await _host.StopAsync();
            _host.Dispose();
        };
        _window.Activate();
    }
}
