using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Shell;

/// <summary>サイドバーの項目に対応するページを作って持つ</summary>
/// <param name="pages">登録されたページ</param>
/// <param name="services">ページを作る DI のサービスプロバイダー</param>
/// <remarks>ページは初回の表示で DI から作り、以後は同じインスタンスを使う（入力中の内容やターミナルを保つため）。</remarks>
public sealed class PageProvider(IEnumerable<NavigationPage> pages, IServiceProvider services)
{
    /// <summary>作ったページ（項目のキー → ページ）</summary>
    private readonly Dictionary<string, Page> _cache = [];

    /// <summary>項目に対応するページを取得する</summary>
    /// <param name="item">サイドバーの項目</param>
    /// <returns>ページ。登録されていない項目なら null</returns>
    public Page? GetPage(NavigationItem item)
    {
        if (_cache.TryGetValue(item.Key, out var page))
        {
            return page;
        }

        if (pages.FirstOrDefault(p => p.Item.Key == item.Key) is not { } registration)
        {
            return null;
        }

        page = (Page)services.GetRequiredService(registration.PageType);
        _cache[item.Key] = page;
        return page;
    }
}
