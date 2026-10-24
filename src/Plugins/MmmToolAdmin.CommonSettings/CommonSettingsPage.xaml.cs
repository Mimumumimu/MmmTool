using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmToolAdmin.CommonSettings;

/// <summary>共通設定のページで、各機能が登録した設定の部品を並べる</summary>
public sealed partial class CommonSettingsPage : Page
{
    /// <summary>ページを作る</summary>
    /// <param name="sections">各機能が登録した設定の部品</param>
    /// <param name="features">機能の並び</param>
    /// <param name="services">部品を作る DI のサービスプロバイダー</param>
    /// <remarks>部品は、並び順の値の順 (同じ値は登録順)に並べる。管理ツールの機能は、オフにできないので、並べ直さない。</remarks>
    public CommonSettingsPage(IEnumerable<SettingsSection> sections, FeatureService features, IServiceProvider services)
    {
        InitializeComponent();

        foreach (var section in sections.OrderBy(s => features.OrderOf(s.FeatureKey, s.Order)))
        {
            SectionsPanel.Children.Add((UIElement)services.GetRequiredService(section.ControlType));
        }
    }
}
