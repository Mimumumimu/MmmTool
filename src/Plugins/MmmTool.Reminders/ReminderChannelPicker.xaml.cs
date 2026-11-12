using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Reminders;

/// <summary>送信先を枠つきのチェックの一覧で選ぶ部品</summary>
/// <remarks>
/// 入力画面とまとめて追加の画面が共有する。
/// 高さは置く側が決める。
/// 入力画面は 3 行ぶんで固定し、まとめて追加は列の残りの高さいっぱいに伸ばす。
/// 選択肢が高さに収まらないときはこの部品の中だけがスクロールする。
/// </remarks>
public sealed partial class ReminderChannelPicker : UserControl
{
    /// <summary>Options の依存関係プロパティ</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(ObservableCollection<ReminderChannelOption>), typeof(ReminderChannelPicker), new PropertyMetadata(null));

    /// <summary>IsEmpty の依存関係プロパティ</summary>
    public static readonly DependencyProperty IsEmptyProperty = DependencyProperty.Register(
        nameof(IsEmpty), typeof(bool), typeof(ReminderChannelPicker), new PropertyMetadata(false));

    /// <summary>部品を作る</summary>
    public ReminderChannelPicker() => InitializeComponent();

    /// <summary>送信先の選択肢</summary>
    public ObservableCollection<ReminderChannelOption>? Options
    {
        get => (ObservableCollection<ReminderChannelOption>?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>選べる送信先が 1 つも無いか</summary>
    /// <remarks>無いときは一覧の中に「送信先が登録されていません。」を出す</remarks>
    public bool IsEmpty
    {
        get => (bool)GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }
}
