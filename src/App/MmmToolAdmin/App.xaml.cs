using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.WinUI.Connection;
using MmmToolAdmin.CodeMaster;
using MmmToolAdmin.Data.SqlServer;
using MmmToolAdmin.Shell;
using MmmToolAdmin.Shell.Main;
using MmmToolAdmin.Users;
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
using MmmSdk.WinUI.Utilities;

namespace MmmToolAdmin;

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
    /// <param name="fatalErrors">復旧できないエラーの報告先 (SDK の部品が使う)</param>
    private static void ConfigureServices(IServiceCollection services, FatalErrorHandler fatalErrors)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(fatalErrors);

        // SDK (JsonFileStore・設定ストア・位置保存・通知ダイアログ・確認ダイアログなど)
        services.AddMmmSdkCore(AppInfo.DataDirectory);
        services.AddMmmSdkWinUI();
        // アプリの名前・データのフォルダー・アイコンの置き場所 (SDK の画面が、アイコンを読むために受け取る)
        services.AddSingleton(new AppEnvironment(AppInfo.Name, AppInfo.DataDirectory, AppInfo.CacheDirectory, AppIcon.FilePath));

        // 機能のオン・オフの管理と、SDK のサイドバー。MmmToolAdmin の機能は、オフにできず、オン・オフの画面も持たない
        services.AddFeatureService();
        // 共通の設定ファイルを、サイドバーの並びを読む前に先読みしておく。最初の読み込みで、UI スレッドが止まらないようにするため
        services.AddStartupTask<SettingsStoreStartup>();
        services.AddNavigationHost();

        // DB (SQL Server)の接続と、ユーザー・登録用コードの保存先。ログインとパスワードの保存名は、MmmTool と同じ
        services.AddMmmToolAdminSqlServer();

        // DB の接続の設定画面。閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddConnectionSettingsDialog();

        // 機能の登録。足すときは、ここに 1 行足すだけで、サイドバーの項目は機能が自分で登録する
        services.AddFeaturePlugin<UsersPlugin>(order: 1);
        services.AddFeaturePlugin<CodeMasterPlugin>(order: 2);

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
            // 起動が途中で止まると、ウィンドウが出ないまま動き続けて気づけない。復旧できないので、ログ・ダイアログ・終了にする
            _fatalErrors.Report("起動に失敗しました", ex);
        }
    }

    /// <summary>Host を始めて、メインウィンドウを出す</summary>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>このアプリは、個々の機能の名前を持たない。機能は <c>AddFeaturePlugin</c> で登録し、サイドバーの項目は機能が自分で登録する。</remarks>
    private async Task StartAsync()
    {
        await _host.StartAsync();

        // 設定ファイルの先読みなどの起動時の準備を、画面を作る前に行う
        await _host.Services.GetRequiredService<FeatureService>().StartAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        // 開いたウィンドウを、メインウィンドウの上に出すため
        _host.Services.GetRequiredService<IDialogHost>().TrackWindow(_window);
        _window.Closed += async (_, _) => await ExitAsync();
        _window.Activate();

        // 接続の設定がまだ無い最初の起動では、接続の設定の画面を自動で開く。閉じたあとの読み直しは、各ページが、画面が閉じた通知を受けて行う
        if (!_host.Services.GetRequiredService<DatabaseSettingsService>().IsModeSaved)
        {
            await _host.Services.GetRequiredService<IConnectionSettingsDialogService>().ShowConnectionSettingsAsync();
        }
    }

    /// <summary>アプリを終了する (メインウィンドウを閉じたとき)</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>開いているウィンドウを閉じ、Host を止めて破棄してから、アプリを終える。</remarks>
    private async Task ExitAsync()
    {
        _host.Services.GetRequiredService<IDialogHost>().CloseAll(_window);
        await _host.StopAsync();
        _host.Dispose();
        Exit();
    }
}
