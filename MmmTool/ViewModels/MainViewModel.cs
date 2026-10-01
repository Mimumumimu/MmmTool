using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public static class Keys
    {
        public const string CliAssist = nameof(CliAssist);
        public const string Settings = nameof(Settings);
    }

    public IReadOnlyList<NavigationItem> MenuItems { get; } =
    [
        new(Keys.CliAssist, "CLI補助", ""),
    ];

    public IReadOnlyList<NavigationItem> FooterItems { get; } =
    [
        new(Keys.Settings, "設定", ""),
    ];

    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }

    public MainViewModel()
    {
        SelectedItem = MenuItems[0];
    }
}
