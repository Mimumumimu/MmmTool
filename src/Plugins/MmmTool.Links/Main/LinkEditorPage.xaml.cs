using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.WinUI.Utilities;
using VirtualKey = Windows.System.VirtualKey;
using Windows.Foundation;

namespace MmmTool.Links.Main;

/// <summary>リンク編集ページ</summary>
public sealed partial class LinkEditorPage : Page
{
    /// <summary>「保存しました」を出しておく時間。</summary>
    private static readonly TimeSpan SavedNoticeDuration = TimeSpan.FromSeconds(3);

    /// <summary>「保存しました」を閉じるタイマー</summary>
    private readonly DispatcherQueueTimer _savedNoticeTimer;

    /// <summary>ページの ViewModel</summary>
    public LinkEditorViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public LinkEditorPage(LinkEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 行 (中身は ListView の行)が右クリックを処理済みにしてからメニューを出すため、XAML の属性での登録では届かない。処理済みでも受け取る
        LinkTree.AddHandler(RightTappedEvent, new RightTappedEventHandler((_, e) => SelectForContextMenu(e.OriginalSource)), handledEventsToo: true);
        LinkTree.AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>((_, e) => SelectForContextMenu(e.OriginalSource)), handledEventsToo: true);

        _savedNoticeTimer = DispatcherQueue.CreateTimer();
        _savedNoticeTimer.Interval = SavedNoticeDuration;
        _savedNoticeTimer.IsRepeating = false;
        _savedNoticeTimer.Tick += (_, _) => SavedInfoBar.IsOpen = false;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.EditNameRequested += (_, _) => FocusNameBox();
        ViewModel.Saved += (_, _) => ShowSavedNotice();
    }

    /// <summary>読み込み時に ViewModel を初期化する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.InitializeAsync();

    #region ツリー

    /// <summary>ツリーの選択が変わったら、ViewModel に反映する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選択の変更の情報</param>
    /// <remarks>TreeView.SelectedItem は x:Bind の双方向にできないため、選択はここで ViewModel と相互に合わせる。</remarks>
    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        => ViewModel.SelectedItem = args.GetAddedItem<LinkTreeItem>();

    /// <summary>ViewModel の選択が変わったら、ツリーの選択を合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LinkEditorViewModel.SelectedItem) && LinkTree.SelectedItem != ViewModel.SelectedItem)
        {
            // 追加直後は親フォルダを開いた行がまだできていないので、表示が追いついてから選ぶ
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => LinkTree.SelectedItem = ViewModel.SelectedItem);
        }
    }

    /// <summary>右クリックした行を選択してからメニューを出す</summary>
    /// <param name="source">右クリックされた要素</param>
    /// <remarks>メニューの操作はその行が対象。何も無い所なら選択を外す。</remarks>
    private void SelectForContextMenu(object source)
    {
        var item = LinkTree.FindItem<LinkTreeItem>(source);
        ViewModel.SelectedItem = item;
        // メニューが出る前に、画面上の選択もその行へ移す (どこに追加されるか見えるように)
        LinkTree.SelectedItem = item;
    }

    /// <summary>ツリーで Delete が押されたら、選択中の項目を削除する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
    private void OnTreeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete && ViewModel.DeleteCommand.CanExecute(null))
        {
            e.Handled = true;
            ViewModel.DeleteCommand.Execute(null);
        }
    }

    #endregion

    /// <summary>名前欄にフォーカスして全選択する</summary>
    /// <remarks>すぐ打ち替えられるように。</remarks>
    private void FocusNameBox()
    {
        // 選択の切り替えで編集欄が表示されてから移す
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            NameBox.Focus(FocusState.Programmatic);
            NameBox.SelectAll();
        });
    }

    #region 保存の通知

    /// <summary>「保存しました」を表示して、一定時間後に閉じる</summary>
    private void ShowSavedNotice()
    {
        SavedNotice.Visibility = Visibility.Visible;
        SavedInfoBar.IsOpen = true;
        _savedNoticeTimer.Stop();
        _savedNoticeTimer.Start();
    }

    /// <summary>「保存しました」が閉じられたときの処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じた理由の情報</param>
    private void OnSavedInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        _savedNoticeTimer.Stop();
        SavedNotice.Visibility = Visibility.Collapsed;
    }

    #endregion
}
