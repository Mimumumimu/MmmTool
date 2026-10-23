using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Foundation;
using Windows.Graphics;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、閉じるまで親を操作できないモーダルで出す。大きさは変えられる。最大化・最小化はできない。
/// 開くときは、黄金比の横長にする。広げたいときは、利用者が縁をドラッグする。大きさは覚えない。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。閉じたウィンドウは再表示できないので、開くたびに作り直す。
/// </remarks>
public sealed partial class QuickMessageWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 800;

    /// <summary>最初の高さ (DIP)</summary>
    /// <remarks>幅との比を、黄金比 (約 1.6 対 1)にする。</remarks>
    private const double InitialHeight = 500;

    /// <summary>最小の幅 (DIP)</summary>
    private const double MinimumWidth = 560;

    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 400;

    /// <summary>閉じたら完了する</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>右クリックした行 (メニューの「削除」の確認を、この行のそばに出す)</summary>
    private TreeViewItem? _contextRow;

    /// <summary>ウィンドウの ViewModel</summary>
    public QuickMessageWindowViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public QuickMessageWindow(QuickMessageWindowViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.EditRequested += (_, _) => FocusEditBox();
        Closed += (_, _) => _closed.TrySetResult();

        // 行の中身が右クリックを処理済みにするので、処理済みでも受け取る
        Tree.AddHandler(UIElement.ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(OnTreeContextRequested), handledEventsToo: true);

        // ドロップの処理の途中では一覧を変えられないので、処理が終わってから、グループの入れ子を直す
        ViewModel.GroupNested += (_, _) => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ViewModel.MoveNestedGroupsOut);
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <returns>閉じるまでの待機を表すタスク。保存した内容は、よく使う文のサービスへ反映済み</returns>
    public async Task ShowModalAsync(Window owner)
    {
        ViewModel.Initialize();

        _modal.SetOwner(owner);
        var scale = _modal.OwnerScale;
        this.UseFixedPresenter(isDialog: true, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));
        // 画面の小さいモニターでも、全体が見えるように、作業領域に収まる高さに抑える
        this.ResizeClientDip(InitialWidth, Math.Min(InitialHeight, _modal.GetAvailableHeightDip()), scale);
        _modal.CenterOnOwner();

        _modal.Show();
        await _closed.Task;
    }

    /// <summary>「保存」が押されたとき、保存できたら閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.SaveAsync())
        {
            _modal.Close();
        }
    }

    /// <summary>「キャンセル」が押されたとき、変更を捨てて閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnCancelClick(object sender, RoutedEventArgs e) => _modal.Close();

    /// <summary>ツリーの選択が変わったら、ViewModel に反映する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選択の変更の情報</param>
    /// <remarks>TreeView.SelectedItem は x:Bind の双方向にできないため、選択はここで ViewModel と相互に合わせる。</remarks>
    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        => ViewModel.SelectedItem = args.GetAddedItem<QuickMessageEditItem>();

    /// <summary>「削除」のボタンが押されたとき、削除を始める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRemoveClick(object sender, RoutedEventArgs e) => RequestRemove(RemoveButton);

    /// <summary>右クリックのメニューの「削除」が押されたとき、削除を始める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRowMenuRemoveClick(object sender, RoutedEventArgs e) => RequestRemove(_contextRow ?? (FrameworkElement)RemoveButton);

    /// <summary>選んでいる項目を削除する。中に文があるグループなら、先に確認を出す</summary>
    /// <param name="target">確認の吹き出しを出す場所 (押したボタン、または右クリックした行)</param>
    /// <remarks>ボタンとメニューで、同じ確認を使う。中に文が無いグループと、文は、確認なしで削除する。</remarks>
    private void RequestRemove(FrameworkElement target)
    {
        if (ViewModel.SelectedItem is { IsGroup: true, Children.Count: > 0 } group)
        {
            RemoveConfirmTitle.Text = $"「{group.DisplayName}」を削除しますか？";
            RemoveConfirmBody.Text = $"中の {group.Children.Count} 件の文もまとめて削除されます。";
            RemoveConfirmFlyout.ShowAt(target);
            return;
        }

        ViewModel.RemoveCommand.Execute(null);
    }

    /// <summary>ツリーの行を右クリックしたとき、その行を選んで、メニューを出す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 何も無い所では、メニューを出さない。
    /// 行の中身 (ListView の行)が右クリックを処理済みにするので、処理済みでも受け取る。
    /// </remarks>
    private void OnTreeContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || Tree.FindContainer(source) is not { } row
            || Tree.ItemFromContainer(row) is not QuickMessageEditItem item)
        {
            return;
        }

        // メニューが出る前に、画面上の選択もその行へ移す (どの行への操作か見えるように)
        ViewModel.SelectedItem = item;
        Tree.SelectedItem = item;
        _contextRow = row;
        if (e.TryGetPosition(row, out var position))
        {
            RowMenu.ShowAt(row, position);
        }
        else
        {
            RowMenu.ShowAt(row);
        }
        e.Handled = true;
    }

    /// <summary>確認の「削除」が押されたら、吹き出しを閉じて削除する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRemoveConfirmed(object sender, RoutedEventArgs e)
    {
        RemoveConfirmFlyout.Hide();
        ViewModel.RemoveCommand.Execute(null);
    }

    /// <summary>確認の「キャンセル」が押されたら、吹き出しを閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRemoveCanceled(object sender, RoutedEventArgs e) => RemoveConfirmFlyout.Hide();

    /// <summary>ドラッグを始めたとき、グループをドラッグしているなら、グループの行へ落とせなくする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">ドラッグの情報</param>
    /// <remarks>
    /// グループの中にグループは入れないため。文をドラッグしているときは、グループの行へ落とせる状態に戻す。
    /// ドラッグの終わりの通知は当てにせず、始めるたびに毎回決め直す。
    /// グループの中の文の間へ落ちたものは、ここでは防げないので、ViewModel が外へ出す。
    /// </remarks>
    private void OnTreeDragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        var draggingGroup = args.Items.OfType<QuickMessageEditItem>().Any(item => item.IsGroup);
        foreach (var group in ViewModel.RootItems.Where(item => item.IsGroup))
        {
            if (Tree.ContainerFromItem(group) is TreeViewItem container)
            {
                container.AllowDrop = !draggingGroup;
            }
        }
    }

    /// <summary>ViewModel の選択が変わったら、ツリーの選択を合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickMessageWindowViewModel.SelectedItem) && Tree.SelectedItem != ViewModel.SelectedItem)
        {
            // 追加直後は行がまだできていないので、表示が追いついてから選ぶ
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => Tree.SelectedItem = ViewModel.SelectedItem);
        }
    }

    /// <summary>選んだ項目の入力欄にフォーカスして、すぐ打てるようにする</summary>
    /// <remarks>グループはグループ名、文は文の欄。選択の切り替えで入力欄が表示されてから移す。</remarks>
    private void FocusEditBox()
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (ViewModel.SelectedItem is { IsGroup: true })
            {
                GroupNameBox.Focus(FocusState.Programmatic);
                GroupNameBox.SelectAll();
            }
            else if (ViewModel.SelectedItem is not null)
            {
                MessageTextBox.Focus(FocusState.Programmatic);
            }
        });
    }
}
