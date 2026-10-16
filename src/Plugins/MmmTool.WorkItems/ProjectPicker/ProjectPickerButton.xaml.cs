using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmTool.WorkItems.ProjectPicker;

/// <summary>「表示する案件」のボタンと、案件を選ぶ入れ物 (フライアウト)。作業管理の表とスケジュールが共有する</summary>
/// <remarks>
/// 使う側が、作ったあとに <see cref="Attach"/> で ViewModel を渡す (XAML から作るので、コンストラクターで受け取れない)。
/// 選んだ案件は、作業管理と同じ設定に保存するので、どちらの画面で変えても、もう一方に反映する。
/// </remarks>
public sealed partial class ProjectPickerButton : UserControl
{
    /// <summary>案件を選ぶ画面の ViewModel (<see cref="Attach"/> で渡す。渡すまでは null)</summary>
    public ProjectPickerViewModel Picker { get; private set; } = null!;

    /// <summary>ボタンを作る</summary>
    public ProjectPickerButton()
    {
        InitializeComponent();
    }

    /// <summary>案件を選ぶ画面の ViewModel を結び付ける</summary>
    /// <param name="picker">案件を選ぶ画面の ViewModel</param>
    public void Attach(ProjectPickerViewModel picker)
    {
        Picker = picker;
        Bindings.Update();
    }

    /// <summary>案件を選ぶ入れ物を開くとき、一覧を読み直す</summary>
    /// <param name="sender">開く入れ物</param>
    /// <param name="e">イベントの情報</param>
    private void OnFlyoutOpening(object? sender, object e) => Picker.OpenAsync().Forget();

    /// <summary>案件のチェックが押されたら、その場で保存する</summary>
    /// <param name="sender">チェックボックス</param>
    /// <param name="e">イベントの情報</param>
    private void OnChoiceClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: ProjectChoice choice } box)
        {
            Picker.SetSelectedAsync(choice, box.IsChecked == true).Forget();
        }
    }

    /// <summary>一覧の最後の行が作られたら、続きを読む</summary>
    /// <param name="sender">一覧</param>
    /// <param name="args">行が作られたときの情報</param>
    private void OnChoiceContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemIndex == Picker.Items.Count - 1)
        {
            Picker.LoadMoreAsync().Forget();
        }
    }
}
