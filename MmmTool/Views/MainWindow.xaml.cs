using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmTool.ViewModels;
using Windows.Graphics;

namespace MmmTool.Views;

public sealed partial class MainWindow : Window
{
    // 画面キー → ページ型
    private static readonly Dictionary<string, Type> PageTypes = new()
    {
        [MainViewModel.Keys.CliAssist] = typeof(CliAssistPage),
        [MainViewModel.Keys.Settings] = typeof(SettingsPage),
    };

    private readonly IServiceProvider _services;

    // ページは初回選択時に DI から生成し、以降は同じインスタンスを使う
    private readonly Dictionary<string, Page> _pageCache = [];

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, IServiceProvider services)
    {
        ViewModel = viewModel;
        _services = services;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new SizeInt32(1280, 720));

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        NavigateTo(ViewModel.SelectedItem);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedItem))
        {
            NavigateTo(ViewModel.SelectedItem);
        }
    }

    private void NavigateTo(NavigationItem? item)
    {
        if (item is null || !PageTypes.TryGetValue(item.Key, out var pageType))
        {
            return;
        }

        if (!_pageCache.TryGetValue(item.Key, out var page))
        {
            page = (Page)_services.GetRequiredService(pageType);
            _pageCache[item.Key] = page;
        }

        ContentFrame.Content = page;
    }
}
