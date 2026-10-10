using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    /// <remarks>部品は並び順の値の順に並べる (機能の部品は、利用者が決めた機能の並び順。同じ値は登録順)。オフの機能の部品は並べず、オン・オフ・並び順が変わったら並べ直す。</remarks>
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
        features.OrderChanged += (_, _) => BuildSections();
    }

    /// <summary>オンの機能の設定の部品を、並び順の値の順に並べ直す</summary>
    /// <remarks>
    /// すでに並んでいる部品は作り直さず、順序の違うものだけを動かす (「機能」の一覧は、ドラッグの直後に作り直さない)。
    /// 並べなくなった部品は外し、新しく並べる部品だけ DI から作る。
    /// </remarks>
    private void BuildSections()
    {
        var desired = _sections
            .Where(s => _features.IsEnabled(s.FeatureKey))
            .OrderBy(s => _features.OrderOf(s.FeatureKey, s.Order))
            .Select(s => s.ControlType)
            .ToList();
        var children = SectionsPanel.Children;

        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(children[i].GetType()))
            {
                children.RemoveAt(i);
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < children.Count && children[i].GetType() == desired[i])
            {
                continue;
            }

            var current = children.ToList().FindIndex(c => c.GetType() == desired[i]);
            if (current >= 0)
            {
                var element = children[current];
                children.RemoveAt(current);
                children.Insert(i, element);
            }
            else
            {
                children.Insert(i, (UIElement)_services.GetRequiredService(desired[i]));
            }
        }
    }
}
