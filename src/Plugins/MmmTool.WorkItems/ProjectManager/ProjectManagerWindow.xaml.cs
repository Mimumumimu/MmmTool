using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;
using Windows.System;

namespace MmmTool.WorkItems.ProjectManager;

/// <summary>案件の管理のウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。大きさは変えられる (最大化・最小化はできない)。
/// タイトル帯をドラッグして移動できる。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// 名前は、普段は文字だけで、クリックすると入力欄になる。既存の案件は、フォーカスが外れたとき・Enter で保存し (Excel と同じ)、Esc で取り消す。追加は、左上の「新しい案件」で出る入力中の行から行い、Enter で確認してから作る (フォーカスが外れただけでは作らない)。削除は、行の右クリックのメニュー (送信先の一覧と同じ)。
/// </remarks>
public sealed partial class ProjectManagerWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 640;

    /// <summary>最初の高さ (DIP)</summary>
    private const double InitialHeight = 520;

    /// <summary>最小の幅 (DIP)</summary>
    private const double MinimumWidth = 480;

    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 360;

    /// <summary>閉じたら完了する</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>閉じてよいと確かめ済みか (確認のあとの Close で、もう一度確認しないため)</summary>
    private bool _closeConfirmed;

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ウィンドウの ViewModel</summary>
    public ProjectManagerViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public ProjectManagerWindow(ProjectManagerViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);
        ViewModel.PendingFocusRequested += OnPendingFocusRequested;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += (_, _) => _closed.TrySetResult();
    }

    /// <summary>閉じる操作 (右上の ×)のとき、作成中の案件があれば、確認する</summary>
    /// <param name="sender">ウィンドウ</param>
    /// <param name="args">閉じる操作の情報</param>
    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed)
        {
            return;
        }

        args.Cancel = true;
        CloseAfterConfirmAsync().Forget();
    }

    /// <summary>確認して、よければ閉じる</summary>
    /// <returns>確認と閉じる処理の完了を表すタスク</returns>
    private async Task CloseAfterConfirmAsync()
    {
        if (await ViewModel.ConfirmCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <returns>閉じるまでの待機を表すタスク</returns>
    public async Task ShowModalAsync(Window owner)
    {
        // 空の一覧が一瞬見えないよう、読み込んでから出す
        await ViewModel.RefreshAsync();

        _modal.SetOwner(owner);
        var scale = _modal.OwnerScale;
        this.UseFixedPresenter(isDialog: true, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));
        this.ResizeClientDip(InitialWidth, InitialHeight, scale);
        _modal.CenterOnOwner();

        _modal.Show();
        await _closed.Task;
    }

    /// <summary>一覧の最後の行が作られたら、続きを読む</summary>
    /// <param name="sender">一覧</param>
    /// <param name="args">行が作られたときの情報</param>
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemIndex == ViewModel.Items.Count - 1)
        {
            ViewModel.LoadMoreAsync().Forget();
        }
    }

    /// <summary>名前の入力欄で Enter なら確定、Esc なら取り消す</summary>
    /// <param name="sender">名前の入力欄</param>
    /// <param name="e">キー入力の情報</param>
    private void OnNameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: ProjectManagerItem item })
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            CommitAsync(item).Forget();
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            if (item.IsNew)
            {
                ViewModel.CancelNew(item);
            }
            else
            {
                item.Name = item.SavedName;
                item.IsEditing = false;
            }
        }
    }

    /// <summary>名前の入力欄からフォーカスが外れたら、確定する</summary>
    /// <param name="sender">名前の入力欄</param>
    /// <param name="e">イベントの情報</param>
    private void OnNameLostFocus(object sender, RoutedEventArgs e)
    {
        // 新しい案件は、Enter で確認してから作る (フォーカスが外れただけでは、保存しない)。既存の案件の名前は、Excel と同じく、フォーカスが外れたら確定する
        if (sender is TextBox { DataContext: ProjectManagerItem { IsNew: false } item })
        {
            ViewModel.RenameAsync(item).Forget();
        }
    }

    /// <summary>名前の文字がクリックされたら、入力欄にして、フォーカスを移す</summary>
    /// <param name="sender">名前の文字を載せた枠</param>
    /// <param name="e">タップの情報</param>
    private void OnNameLabelTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { DataContext: ProjectManagerItem item, Parent: Grid grid })
        {
            item.IsEditing = true;
            if (grid.Children.OfType<TextBox>().FirstOrDefault() is { } box)
            {
                box.UpdateLayout();
                box.Focus(FocusState.Programmatic);
                box.SelectAll();
            }
        }
    }

    /// <summary>入力中の行の入力欄が作られたら、フォーカスを移す</summary>
    /// <param name="sender">名前の入力欄</param>
    /// <param name="e">イベントの情報</param>
    private void OnNameLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ProjectManagerItem { IsNew: true } } box)
        {
            box.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>入力中の行にフォーカスを移す (すでに出ている行へ、もう一度「新しい案件」を押したとき)</summary>
    /// <param name="item">入力中の行</param>
    private void OnPendingFocusRequested(ProjectManagerItem item)
    {
        ProjectListView.ScrollIntoView(item);
        (ProjectListView.ContainerFromItem(item) as ListViewItem)?.Focus(FocusState.Programmatic);
        if (ProjectListView.ContainerFromItem(item) is ListViewItem { ContentTemplateRoot: Grid grid } && grid.Children.OfType<TextBox>().FirstOrDefault() is { } box)
        {
            box.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>名前の入力を確定する (入力中の行は追加、そのほかは名前の変更)</summary>
    /// <param name="item">一覧の 1 件</param>
    /// <returns>確定の完了を表すタスク</returns>
    private Task CommitAsync(ProjectManagerItem item) => item.IsNew ? ViewModel.CreateAsync(item) : ViewModel.RenameAsync(item);

    /// <summary>メニューの「削除」が押されたら、確認してから削除する</summary>
    /// <param name="sender">メニュー項目 (行は Tag に入れてある)</param>
    /// <param name="e">イベントの情報</param>
    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is ProjectManagerItem item)
        {
            ViewModel.DeleteAsync(item).Forget();
        }
    }
}
