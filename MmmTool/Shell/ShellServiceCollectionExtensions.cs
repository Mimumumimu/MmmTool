using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI;
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
        // 機能のオン・オフ (状態の保存・起動時の準備の実行・切り替えの通知)
        services.AddSingleton<FeatureService>();
        // 共通の設定ファイルを、各機能の準備より先に読んでおく (各機能が最初に設定を読むとき、UI スレッドで止まらないように)
        services.AddStartupTask<SettingsStoreStartup>();
        // メニューの項目は、各機能が登録した ITrayMenuSource から作る
        services.AddMmmSdkTray(new TrayIconOptions(
            ToolTip: AppInfo.Name,
            WindowClassName: AppInfo.TrayWindowClassName,
            ExitText: "終了",
            IconPath: AppIcon.FilePath));
        return services;
    }

    /// <summary>サイドバーにページを登録する</summary>
    /// <typeparam name="TPage">表示するページの型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="title">サイドバーの表示名</param>
    /// <param name="glyph">サイドバーのアイコン (Segoe Fluent Icons のグリフ)</param>
    /// <param name="area">サイドバーの中で出す場所</param>
    /// <param name="featureKey">属する機能のキー (<see cref="AddFeature"/> で登録したもの)。オフにできない機能は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>項目は場所ごとに登録した順に並ぶ。項目のキーはページの型名。機能がオフの間は出さない。</remarks>
    public static IServiceCollection AddNavigationPage<TPage>(this IServiceCollection services, string title, string glyph, NavigationArea area, string? featureKey = null)
        where TPage : Page
    {
        services.AddTransient<TPage>();
        services.AddSingleton(new NavigationPage(new NavigationItem(typeof(TPage).Name, title, glyph), typeof(TPage), area, featureKey));
        return services;
    }

    /// <summary>設定ページに、機能ごとの設定の部品を登録する</summary>
    /// <typeparam name="TControl">設定の部品 (<c>UserControl</c>)の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<see cref="AddFeature"/> で登録したもの)。オフにできない機能は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>部品は設定ページを開くときに DI から作る (Transient)。登録した順に並ぶ。値の読み書きと画面の状態は、その機能の ViewModel・サービスが持つ。機能がオフの間は並べない。</remarks>
    public static IServiceCollection AddSettingsSection<TControl>(this IServiceCollection services, string? featureKey = null)
        where TControl : UserControl
    {
        services.AddTransient<TControl>();
        services.AddSingleton(new SettingsSection(typeof(TControl), featureKey));
        return services;
    }

    /// <summary>トレイメニューに項目を出す機能を登録する</summary>
    /// <typeparam name="TSource">トレイメニューの項目の元の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<see cref="AddFeature"/> で登録したもの)。オフにできない機能は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>登録した順に、区切り線で分けて並ぶ。機能がオフの間は項目を出さない (<see cref="FeatureTrayMenuSource"/>)。</remarks>
    public static IServiceCollection AddTrayMenuSource<TSource>(this IServiceCollection services, string? featureKey = null)
        where TSource : class, ITrayMenuSource
    {
        if (featureKey is null)
        {
            return services.AddSingleton<ITrayMenuSource, TSource>();
        }

        services.AddSingleton<TSource>();
        return services.AddSingleton<ITrayMenuSource>(provider => new FeatureTrayMenuSource(
            featureKey,
            provider.GetRequiredService<TSource>(),
            provider.GetRequiredService<FeatureService>()));
    }

    /// <summary>設定でオン・オフを切り替えられる機能として登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">機能のキー (設定ファイルにも使う。決めたら変えない)</param>
    /// <param name="displayName">設定ページに出す機能の名前</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 機能の <c>Add&lt;機能&gt;()</c> の中で、ページ・トレイメニュー・起動時の準備などの登録に、同じキーを渡す。
    /// 設定ページの「機能」の一覧に、登録順に並ぶ。
    /// </remarks>
    public static IServiceCollection AddFeature(this IServiceCollection services, string featureKey, string displayName)
        => services.AddSingleton(new FeatureInfo(featureKey, displayName));

    /// <summary>起動時の準備を登録する</summary>
    /// <typeparam name="TTask">起動時の準備の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<see cref="AddFeature"/> で登録したもの)。オフにできない機能・共通の準備は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>登録した順に実行される。機能がオフの間は実行しない (オンにしたときに実行する)。</remarks>
    public static IServiceCollection AddStartupTask<TTask>(this IServiceCollection services, string? featureKey = null)
        where TTask : class, IStartupTask
    {
        services.AddSingleton<TTask>();
        return services.AddSingleton(new StartupTaskRegistration(typeof(TTask), featureKey));
    }
}
