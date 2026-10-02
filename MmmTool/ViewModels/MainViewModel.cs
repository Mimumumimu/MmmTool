using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.ViewModels;

/// <summary>メイン画面（ナビゲーション）の ViewModel</summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>ナビゲーション項目のキー</summary>
    public static class Keys
    {
        /// <summary>CLI補助</summary>
        public const string CliAssist = nameof(CliAssist);
        /// <summary>リンク</summary>
        public const string Links = nameof(Links);
        /// <summary>設定</summary>
        public const string Settings = nameof(Settings);
        /// <summary>DEBUG（デバッグビルドだけ）</summary>
        public const string Debug = nameof(Debug);
    }

    /// <summary>サイドバー上部の項目</summary>
    public IReadOnlyList<NavigationItem> MenuItems { get; } =
    [
        new(Keys.CliAssist, "CLI補助", ""),
        new(Keys.Links, "リンク", ""),
    ];

    /// <summary>サイドバー下部の項目</summary>
    public IReadOnlyList<NavigationItem> FooterItems { get; } =
    [
#if DEBUG
        new(Keys.Debug, "DEBUG", ""),
#endif
        new(Keys.Settings, "設定", ""),
    ];

    /// <summary>選択中の項目</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }

    /// <summary>最初の項目（CLI補助）を選択した状態にする</summary>
    public MainViewModel()
    {
        SelectedItem = MenuItems[0];
    }
}
