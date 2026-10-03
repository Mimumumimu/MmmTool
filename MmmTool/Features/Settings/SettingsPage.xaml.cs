using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmTool.Shell;

namespace MmmTool.Features.Settings;

/// <summary>設定ページ</summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    /// <param name="sections">各機能が登録した設定の部品</param>
    /// <param name="services">部品を作る DI のサービスプロバイダー</param>
    /// <remarks>部品は登録順に並べる。</remarks>
    public SettingsPage(SettingsViewModel viewModel, IEnumerable<SettingsSection> sections, IServiceProvider services)
    {
        ViewModel = viewModel;
        InitializeComponent();

        foreach (var section in sections)
        {
            SectionsPanel.Children.Add((UIElement)services.GetRequiredService(section.ControlType));
        }
    }
}
