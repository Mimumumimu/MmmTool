using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Tray;

namespace MmmTool.Shell;

/// <summary>画面の枠（メインウィンドウ・サイドバー・トレイ）の DI 登録</summary>
public static class ShellServiceCollectionExtensions
{
    /// <summary>画面の枠を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    public static IServiceCollection AddShell(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PageProvider>();
        // メニューの項目は、各機能が登録した ITrayMenuSource から作る
        services.AddMmmSdkTray(new TrayIconOptions(
            ToolTip: "MmmTool",
            WindowClassName: "MmmTool_Tray",
            IconPath: AppIcon.FilePath));
        return services;
    }

    /// <summary>サイドバーにページを登録する</summary>
    /// <typeparam name="TPage">表示するページの型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="title">サイドバーの表示名</param>
    /// <param name="glyph">サイドバーのアイコン（Segoe Fluent Icons のグリフ）</param>
    /// <param name="area">サイドバーの中で出す場所</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>項目は場所ごとに登録した順に並ぶ。項目のキーはページの型名。</remarks>
    public static IServiceCollection AddNavigationPage<TPage>(this IServiceCollection services, string title, string glyph, NavigationArea area)
        where TPage : Page
    {
        services.AddTransient<TPage>();
        services.AddSingleton(new NavigationPage(new NavigationItem(typeof(TPage).Name, title, glyph), typeof(TPage), area));
        return services;
    }

    /// <summary>起動時の準備を登録する</summary>
    /// <typeparam name="TTask">起動時の準備の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>登録した順に実行される。</remarks>
    public static IServiceCollection AddStartupTask<TTask>(this IServiceCollection services)
        where TTask : class, IStartupTask
        => services.AddSingleton<IStartupTask, TTask>();
}
