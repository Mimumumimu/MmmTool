using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.WinUI.Connection;
using MmmToolAdmin.CodeMaster;
using MmmToolAdmin.CommonSettings;
using MmmToolAdmin.Data.SqlServer;
using MmmToolAdmin.Shell;
using MmmToolAdmin.Shell.Main;
using MmmToolAdmin.Users;
using MmmSdk.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.Settings;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Hosting;
using MmmSdk.WinUI.Components.Pages;

namespace MmmToolAdmin;

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
        services.AddFeaturePlugin<CommonSettingsPlugin>(order: 2);
        services.AddFeaturePlugin<CodeMasterPlugin>(order: 3);

        services.AddSingleton<MainWindow>();
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _appHost.LaunchAsync(StartAsync);
    }

    /// <summary>Host を始めてメインウィンドウを出す</summary>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>
    /// このアプリは個々の機能の名前を持たない。
    /// 機能は <c>AddFeaturePlugin</c> で登録し、サイドバーの項目は機能が自分で登録する。
    /// </remarks>
    private async Task StartAsync()
    {
        await _appHost.StartHostAsync();
        var services = _appHost.Services;

        // 設定ファイルの先読みなどの起動時の準備を、画面を作る前に行う
        await services.GetRequiredService<FeatureService>().StartAsync();

        _window = services.GetRequiredService<MainWindow>();
        // 開いたウィンドウを、メインウィンドウの上に出すため
        services.GetRequiredService<IDialogHost>().TrackWindow(_window);
        _window.Closed += async (_, _) => await ExitAsync();
        _window.Activate();

        // 接続の設定がまだ無い最初の起動では、接続の設定の画面を自動で開く。閉じたあとの読み直しは、各ページが、画面が閉じた通知を受けて行う
        if (!services.GetRequiredService<DatabaseSettingsService>().IsModeSaved)
        {
            await services.GetRequiredService<IConnectionSettingsDialogService>().ShowConnectionSettingsAsync();
        }
    }

    /// <summary>アプリを終了する</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>
    /// メインウィンドウを閉じたとき。
    /// 開いているウィンドウを閉じ、Host を止めて破棄してから、アプリを終える。
    /// 二重の呼び出しの防止は <see cref="AppHost.ExitAsync"/> が持つ。
    /// </remarks>
    private Task ExitAsync() => _appHost.ExitAsync(_window);
}
