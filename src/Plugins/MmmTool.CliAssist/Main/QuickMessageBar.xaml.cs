using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.CliAssist.Main;

/// <summary>入力欄の中に並べる、よく使う文のチップの行</summary>
/// <remarks>
/// チップは幅に収まる分だけ並べ、収まらない分は右端の「⋯」の一覧に出す。
/// グループのチップと、「⋯」の一覧の中のグループは、押すと中の文の一覧が開く。
/// </remarks>
public sealed partial class QuickMessageBar : UserControl
{
    /// <summary>並べる文とグループの依存関係プロパティ</summary>
    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(
        nameof(Messages), typeof(IReadOnlyList<QuickMessageItem>), typeof(QuickMessageBar), new PropertyMetadata(null));

    /// <summary>編集のコマンドの依存関係プロパティ</summary>
    public static readonly DependencyProperty EditCommandProperty = DependencyProperty.Register(
        nameof(EditCommand), typeof(ICommand), typeof(QuickMessageBar), new PropertyMetadata(null));

    /// <summary>行を作る</summary>
    public QuickMessageBar()
    {
        InitializeComponent();

        // 配置の途中で呼ばれるので、ボタンの表示は配置が終わってから変える
        ChipsLayout.OverflowChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateOverflowButton);
    }

    /// <summary>文が選ばれた (チップ・グループの一覧・「⋯」の一覧のどれからでも)</summary>
    public event EventHandler<QuickMessageItem>? MessageChosen;

    /// <summary>並べる文とグループ</summary>
    public IReadOnlyList<QuickMessageItem>? Messages
    {
        get => (IReadOnlyList<QuickMessageItem>?)GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    /// <summary>よく使う文を編集するコマンド</summary>
    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    /// <summary>「⋯」を、隠したチップがあるときだけ出す</summary>
    private void UpdateOverflowButton()
        => OverflowButton.Visibility = ChipsLayout.HasHidden ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>文のチップが押されたら、その文を選ぶ</summary>
    /// <param name="sender">イベントの送信元 (チップのボタン)</param>
    /// <param name="e">イベントの情報</param>
    private void OnChipClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is QuickMessageItem item)
        {
            MessageChosen?.Invoke(this, item);
        }
    }

    /// <summary>グループの一覧を開くとき、中の文を並べる</summary>
    /// <param name="sender">イベントの送信元 (グループの一覧)</param>
    /// <param name="e">イベントの情報</param>
    private void OnGroupFlyoutOpening(object? sender, object e)
    {
        if (sender is MenuFlyout { Target.Tag: QuickMessageItem { Items: { } items } } flyout)
        {
            flyout.Items.Clear();
            AddMenuItems(flyout.Items, items);
        }
    }

    /// <summary>「⋯」の一覧を開くとき、隠れているチップを並べる</summary>
    /// <param name="sender">イベントの送信元 (「⋯」の一覧)</param>
    /// <param name="e">イベントの情報</param>
    private void OnOverflowFlyoutOpening(object? sender, object e)
    {
        if (sender is MenuFlyout flyout)
        {
            flyout.Items.Clear();
            AddMenuItems(flyout.Items, (Messages ?? []).Skip(ChipsLayout.HiddenStartIndex));
        }
    }

    /// <summary>一覧に、文とグループの項目を足す</summary>
    /// <param name="target">足す先の一覧</param>
    /// <param name="items">足す文とグループ</param>
    /// <remarks>グループは、中の文を持つサブメニューにする。</remarks>
    private void AddMenuItems(IList<MenuFlyoutItemBase> target, IEnumerable<QuickMessageItem> items)
    {
        foreach (var item in items)
        {
            if (item.Items is { } children)
            {
                var group = new MenuFlyoutSubItem { Text = item.Label };
                AddMenuItems(group.Items, children);
                target.Add(group);
                continue;
            }

            var menuItem = new MenuFlyoutItem { Text = item.Label, Tag = item };
            ToolTipService.SetToolTip(menuItem, item.Text);
            menuItem.Click += OnMenuItemClick;
            target.Add(menuItem);
        }
    }

    /// <summary>一覧の項目が押されたら、その文を選ぶ</summary>
    /// <param name="sender">イベントの送信元 (一覧の項目)</param>
    /// <param name="e">イベントの情報</param>
    private void OnMenuItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is QuickMessageItem item)
        {
            MessageChosen?.Invoke(this, item);
        }
    }
}
