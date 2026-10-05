namespace MmmTool.Shell;

/// <summary>サイドバーに出すページの登録情報</summary>
/// <param name="Item">サイドバーの項目</param>
/// <param name="PageType">表示するページの型 (DI から作る)</param>
/// <param name="Area">サイドバーの中で出す場所</param>
/// <param name="FeatureKey">属する機能のキー。オフにできない機能は null</param>
/// <remarks>各機能が <see cref="ShellServiceCollectionExtensions.AddNavigationPage{TPage}"/> で登録し、登録した順に並ぶ。機能がオフの間は出さない。</remarks>
public sealed record NavigationPage(NavigationItem Item, Type PageType, NavigationArea Area, string? FeatureKey);
