using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.Shell.Main;

namespace MmmTool.Shell;

/// <summary>画面の枠 (メインウィンドウ・サイドバー・トレイ)の DI 登録</summary>
public static class ShellServiceCollectionExtensions
{
    /// <summary>画面の枠を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    public static IServiceCollection AddShell(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PageProvider>();
        // 画面から、アプリの終了 (トレイの「終了」と同じ処理)を頼む口
        services.AddSingleton<AppExitService>();
        // 機能 (プラグイン)が、作ったページを調べる口
        services.AddSingleton<IPageCache>(provider => provider.GetRequiredService<PageProvider>());
        // 機能 (プラグイン)が、アプリの名前・データ・アイコンの置き場所を知る口
        services.AddSingleton(new AppEnvironment(AppInfo.Name, AppInfo.DataDirectory, AppIcon.FilePath));
        // メインウィンドウの設定 (起動時に開くか)
        services.AddSingleton<MainWindowSettingsService>();
        // 機能のオン・オフ (状態の保存・起動時の準備の実行・切り替えの通知)と、機能 (プラグイン)が自分のキーのオン・オフを調べる口
        services.AddFeatureService();
        // 共通の設定ファイルを、各機能の準備より先に読んでおく (各機能が最初に設定を読むとき、UI スレッドで止まらないように)
        services.AddStartupTask<SettingsStoreStartup>();
        // 音声機器を眠らせない無音の出力 (設定と、起動時に始める準備)
        services.AddSingleton<AudioKeepAliveSettingsService>();
        services.AddStartupTask<AudioKeepAliveStartup>();
        // メニューの項目は、各機能が登録した ITrayMenuSource から作る
        services.AddMmmSdkTray(new TrayIconOptions(
            ToolTip: AppInfo.Name,
            WindowClassName: AppInfo.TrayWindowClassName,
            ExitText: "終了",
            IconPath: AppIcon.FilePath));
        return services;
    }
}
