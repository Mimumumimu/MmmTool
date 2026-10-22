using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Reminders;

/// <summary>
/// 送信先を、枠つきのチェックの一覧で選ぶ部品。入力画面とまとめて追加の画面が共有する。
/// </summary>
/// <remarks>
/// 高さは置く側が決める。入力画面は 3 行ぶんで固定し、まとめて追加は、列の残りの高さいっぱいに伸ばす。
/// 選択肢が高さに収まらないときは、この部品の中だけがスクロールする。
/// </remarks>
public sealed partial class ReminderChannelPicker : UserControl
{
    /// <summary><see cref="Options"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(ObservableCollection<ReminderChannelOption>), typeof(ReminderChannelPicker), new PropertyMetadata(null));

    /// <summary><see cref="IsEmpty"/> の依存関係プロパティ</summary>
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

    /// <summary>選べる送信先が 1 つも無いか (無いときは、一覧の中に「送信先が登録されていません。」を出す)</summary>
    public bool IsEmpty
    {
        get => (bool)GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }
}
