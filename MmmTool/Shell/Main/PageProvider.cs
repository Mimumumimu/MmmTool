using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Components.Pages;

namespace MmmTool.Shell.Main;

/// <summary>サイドバーの項目に対応するページを作って持つ</summary>
/// <param name="pages">登録されたページ</param>
/// <param name="features">機能のオン・オフ</param>
/// <param name="scopeFactory">ページごとのスコープを作る</param>
/// <remarks>
/// ページは初回の表示で、ページごとのスコープから DI で作り、以後は同じインスタンスを使う (入力中の内容やターミナルを保つため)。
/// 機能がオフの間はページを作らず、オフにしたときは、作ったページを捨てる (<see cref="EvictDisabledPages"/>)。
/// スコープごとに作るのは、捨てたページが持つ <c>IDisposable</c>(ターミナルのセッションなど)を、スコープの破棄で解放するため
/// (ルートのプロバイダーから作ると、オン・オフのたびに増えて、Host の破棄まで残る)。残りのスコープは、この <c>Dispose</c>(Host の破棄)で破棄する。
/// </remarks>
public sealed class PageProvider(IEnumerable<NavigationPage> pages, FeatureService features, IServiceScopeFactory scopeFactory) : IPageCache, IDisposable
{
    /// <summary>作ったページと、それを作ったスコープ (項目のキー → ページ・スコープ)</summary>
    private readonly Dictionary<string, (Page Page, IServiceScope Scope)> _cache = [];

    /// <summary>項目に対応するページを取得する</summary>
    /// <param name="item">サイドバーの項目</param>
    /// <returns>ページ。登録されていない項目・機能がオフの項目なら null</returns>
    public Page? GetPage(NavigationItem item)
    {
        if (_cache.TryGetValue(item.Key, out var cached))
        {
            return cached.Page;
        }

        if (pages.FirstOrDefault(p => p.Item.Key == item.Key) is not { } registration
            || !features.IsEnabled(registration.FeatureKey))
        {
            return null;
        }

        var scope = scopeFactory.CreateScope();
        var page = (Page)scope.ServiceProvider.GetRequiredService(registration.PageType);
        _cache[item.Key] = (page, scope);
        return page;
    }

    /// <inheritdoc />
    public TPage? GetCreatedPage<TPage>()
        where TPage : Page
        => _cache.TryGetValue(typeof(TPage).Name, out var cached) ? cached.Page as TPage : null;

    /// <summary>オフになった機能のページを捨てる</summary>
    /// <remarks>
    /// 画面の後始末が要るページ (<see cref="IReleasablePage"/>)は、先に <c>Release</c> を呼び、そのあと、ページを作ったスコープを破棄する (ターミナルのセッションなどを解放する)。
    /// 画面は、このページから別のページへ移したあとに呼ぶこと (表示中のページを捨てないため)。
    /// 再びオンにしたときは、新しく作る。
    /// </remarks>
    public void EvictDisabledPages()
    {
        foreach (var registration in pages.Where(p => !features.IsEnabled(p.FeatureKey)))
        {
            if (_cache.Remove(registration.Item.Key, out var cached))
            {
                (cached.Page as IReleasablePage)?.Release();
                cached.Scope.Dispose();
            }
        }
    }

    /// <summary>作ったページのスコープをすべて破棄する (Host の破棄時)</summary>
    public void Dispose()
    {
        foreach (var (_, scope) in _cache.Values)
        {
            scope.Dispose();
        }
        _cache.Clear();
    }
}
