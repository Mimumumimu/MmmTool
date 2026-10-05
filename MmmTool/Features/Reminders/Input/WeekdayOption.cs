using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Core.Reminders;

namespace MmmTool.Features.Reminders.Input;

/// <summary>リマインダー入力の曜日の選択肢 1 つ (月〜日のトグルボタン)</summary>
/// <param name="flag">曜日フラグ</param>
/// <param name="label">表示名 (「月」など)</param>
public sealed partial class WeekdayOption(Weekdays flag, string label) : ObservableObject
{
    /// <summary>曜日フラグ</summary>
    public Weekdays Flag { get; } = flag;

    /// <summary>表示名</summary>
    public string Label { get; } = label;

    /// <summary>選ばれているか</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
