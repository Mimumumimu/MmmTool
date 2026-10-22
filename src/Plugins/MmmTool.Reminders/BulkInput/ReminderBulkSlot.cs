using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.Reminders.BulkInput;

/// <summary>
/// まとめて追加の時刻の表の、1 つのマス。30 分刻みの時刻 1 つに当たる。
/// </summary>
/// <param name="time">マスの時刻</param>
public sealed partial class ReminderBulkSlot(TimeOnly time) : ObservableObject
{
    /// <summary>マスの時刻</summary>
    public TimeOnly Time { get; } = time;

    /// <summary>選ばれているか</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
