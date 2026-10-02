using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmTool.Interop;
using MmmTool.ViewModels;
using Windows.Graphics;

namespace MmmTool.Views;

public sealed partial class MainWindow : Window
{
    /// <summary>画面キーに対応するページ型。</summary>
    private static readonly Dictionary<string, Type> PageTypes = new()
    {
        [MainViewModel.Keys.CliAssist] = typeof(CliAssistPage),
        [MainViewModel.Keys.Links] = typeof(LinkEditorPage),
        [MainViewModel.Keys.Settings] = typeof(SettingsPage),
    };

    private readonly IServiceProvider _services;

    /// <summary>生成済みページのキャッシュ。</summary>
    /// <remarks>ページは初回選択時に DI から生成し、以降は同じインスタンスを使う。</remarks>
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
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        AppWindow.Closing += OnClosing;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        NavigateTo(ViewModel.SelectedItem);
    }

    #region トレイへの退避・再表示

    /// <summary>アプリを終了中か（閉じる要求が「本当の終了」か「トレイへの退避」かを見分ける）。</summary>
    private bool _isExiting;

    /// <summary>×ボタン・Alt+F4 では終了せず、トレイへ退避する（非表示にするだけで、アプリは動き続ける）。</summary>
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;
        args.Cancel = true;
        sender.Hide();
    }

    /// <summary>以降の閉じる要求で本当に閉じるようにする（トレイの「終了」から）。</summary>
    public void PrepareExit() => _isExiting = true;

    /// <summary>トレイ・最小化から確実に戻して前面に出す（復元 → 表示 → 前面化）。</summary>
    public void ShowAndActivate()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }
        AppWindow.Show(activateWindow: true);
        Activate();
        NativeMethods.SetForegroundWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id));
    }

    #endregion

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
