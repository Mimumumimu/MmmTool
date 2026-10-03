using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.Shell;

/// <summary>メイン画面（ナビゲーション）の ViewModel</summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>登録されたページから、サイドバーの項目を作る</summary>
    /// <param name="pages">各機能が登録したページ（登録順）</param>
    /// <remarks>最初は上部の先頭の項目を選択した状態にする。</remarks>
    public MainViewModel(IEnumerable<NavigationPage> pages)
    {
        MenuItems = [.. pages.Where(p => p.Area == NavigationArea.Top).Select(p => p.Item)];
        FooterItems = [.. pages.Where(p => p.Area == NavigationArea.Footer).Select(p => p.Item)];
        SelectedItem = MenuItems.FirstOrDefault();
    }

    /// <summary>サイドバー上部の項目</summary>
    public IReadOnlyList<NavigationItem> MenuItems { get; }

    /// <summary>サイドバー下部の項目</summary>
    public IReadOnlyList<NavigationItem> FooterItems { get; }

    /// <summary>選択中の項目</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }
}
