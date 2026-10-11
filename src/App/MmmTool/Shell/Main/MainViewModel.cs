using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmTool.Shell.Main;

/// <summary>メイン画面 (ナビゲーション)の ViewModel</summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>各機能が登録したページ (登録順)</summary>
    private readonly IReadOnlyList<NavigationPage> _pages;

    /// <summary>機能のオン・オフ</summary>
    private readonly FeatureService _features;

    /// <summary>登録されたページから、サイドバーの項目を作る</summary>
    /// <param name="pages">各機能が登録したページ</param>
    /// <param name="features">機能のオン・オフ</param>
    /// <remarks>オンの機能の項目だけを並べる。最初は上部の先頭の項目を選択した状態にする。</remarks>
    public MainViewModel(IEnumerable<NavigationPage> pages, FeatureService features)
    {
        _pages = [.. pages];
        _features = features;
        MenuItems = [];
        FooterItems = [];
        Refresh();
        SelectedItem = MenuItems.FirstOrDefault();
    }

    /// <summary>サイドバー上部の項目</summary>
    public ObservableCollection<NavigationItem> MenuItems { get; }

    /// <summary>サイドバー下部の項目</summary>
    public ObservableCollection<NavigationItem> FooterItems { get; }

    /// <summary>選択中の項目</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }

    /// <summary>機能のオン・オフ・並び順に合わせて、サイドバーの項目を作り直す</summary>
    /// <remarks>選択中の項目が消えるときは、先に上部の先頭の項目へ移す (ページの切り替えは、選択の変更で行われる)。</remarks>
    public void Refresh()
    {
        var top = EnabledItems(NavigationArea.Top);
        var footer = EnabledItems(NavigationArea.Footer);

        if (SelectedItem is { } selected && !top.Contains(selected) && !footer.Contains(selected))
        {
            SelectedItem = top.FirstOrDefault();
        }

        Sync(MenuItems, top, SelectedItem);
        Sync(FooterItems, footer, SelectedItem);
    }

    /// <summary>場所ごとの、オンの機能の項目を取得する</summary>
    /// <param name="area">サイドバーの中の場所</param>
    /// <returns>並び順の項目 (機能の並びは利用者が決めた順。同じ値は登録順)</returns>
    private List<NavigationItem> EnabledItems(NavigationArea area)
        => [.. _pages.Where(p => p.Area == area && _features.IsEnabled(p.FeatureKey))
            .OrderBy(p => _features.OrderOf(p.FeatureKey, p.Order))
            .Select(p => p.Item)];

    /// <summary>一覧を、足りない項目を足し、余る項目を消し、順序が違うときは、選択中の項目のまわりに並べ直して合わせる</summary>
    /// <param name="target">合わせる一覧 (画面にバインドしているので、作り直さず、項目ごとに足し引きする)</param>
    /// <param name="desired">あるべき項目 (順序つき)</param>
    /// <param name="selected">選択中の項目 (外すと選択が外れるので、残したまま、まわりを並べ直す)</param>
    /// <remarks>
    /// 並べ替えに <c>Move</c> は使わない (<c>NavigationView</c> が、移動の通知を画面に反映しなかったため)。
    /// 追加と削除の通知だけで並べ直す。
    /// </remarks>
    private static void Sync(ObservableCollection<NavigationItem> target, List<NavigationItem> desired, NavigationItem? selected)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        // 足りない項目を足したあとの並びが、あるべき並びと同じなら、何もしない
        var missing = desired.Where(d => !target.Contains(d)).ToList();
        if (missing.Count == 0 && target.SequenceEqual(desired))
        {
            return;
        }

        var keep = selected is not null && desired.Contains(selected) && target.Contains(selected) ? selected : null;
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (target[i] != keep)
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (desired[i] != keep)
            {
                target.Insert(i, desired[i]);
            }
        }
    }
}
