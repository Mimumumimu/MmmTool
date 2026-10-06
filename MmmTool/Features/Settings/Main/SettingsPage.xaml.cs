using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmTool.Features.Settings.Main;

/// <summary>設定ページ</summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>各機能が登録した設定の部品</summary>
    private readonly IReadOnlyList<SettingsSection> _sections;

    /// <summary>機能のオン・オフ</summary>
    private readonly FeatureService _features;

    /// <summary>部品を作る DI のサービスプロバイダー</summary>
    private readonly IServiceProvider _services;

    /// <summary>ページの ViewModel</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    /// <param name="sections">各機能が登録した設定の部品</param>
    /// <param name="features">機能のオン・オフ</param>
    /// <param name="services">部品を作る DI のサービスプロバイダー</param>
    /// <remarks>部品は登録順に並べる。オフの機能の部品は並べず、オン・オフが切り替わったら並べ直す。</remarks>
    public SettingsPage(SettingsViewModel viewModel, IEnumerable<SettingsSection> sections, FeatureService features, IServiceProvider services)
    {
        ViewModel = viewModel;
        _sections = [.. sections];
        _features = features;
        _services = services;
        InitializeComponent();

        BuildSections();
        features.Changed += (_, featureKey) =>
        {
            // 設定の部品を持つ機能のときだけ並べ直す (ほかの部品を作り直さない)
            if (_sections.Any(s => s.FeatureKey == featureKey))
            {
                BuildSections();
            }
        };
    }

    /// <summary>オンの機能の設定の部品を、登録順に並べ直す</summary>
    private void BuildSections()
    {
        SectionsPanel.Children.Clear();
        foreach (var section in _sections.Where(s => _features.IsEnabled(s.FeatureKey)))
        {
            SectionsPanel.Children.Add((UIElement)_services.GetRequiredService(section.ControlType));
        }
    }

    /// <summary>機能のスイッチが切り替わったときの処理</summary>
    /// <param name="sender">イベントの送信元 (スイッチ。データの行を持つ)</param>
    /// <param name="e">イベントの情報</param>
    private async void OnFeatureToggled(object sender, RoutedEventArgs e)
    {
        // バインドの書き戻しより先に呼ばれることがあるので、スイッチの状態を行へ写してから渡す
        if (sender is ToggleSwitch { DataContext: FeatureItem item } toggle)
        {
            item.IsOn = toggle.IsOn;
            await ViewModel.ToggleFeatureAsync(item);
        }
    }
}
