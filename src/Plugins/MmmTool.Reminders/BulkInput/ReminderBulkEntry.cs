using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.BulkInput;

/// <summary>
/// まとめて追加の右の一覧の、選んだ会議 1 件。時刻・件名・リンクを直せる。
/// </summary>
/// <param name="slot">この会議を選んだ、時刻の表のマス</param>
public sealed partial class ReminderBulkEntry(ReminderBulkSlot slot) : ObservableObject
{
    /// <summary>この会議を選んだ、時刻の表のマス</summary>
    /// <remarks>マスの選びを外すと、この会議も外れる。時刻を直しても、結びつきは変わらない。</remarks>
    public ReminderBulkSlot Slot { get; } = slot;

    /// <summary>開始時刻の時 (0〜23)</summary>
    [ObservableProperty]
    public partial int Hour { get; set; } = slot.Time.Hour;

    /// <summary>開始時刻の分 (0〜59)</summary>
    [ObservableProperty]
    public partial int Minute { get; set; } = slot.Time.Minute;

    /// <summary>件名。行が増えたときは「会議」が入っている</summary>
    /// <remarks>空にしても、登録のとき「会議」になる。</remarks>
    [ObservableProperty]
    public partial string Title { get; set; } = ReminderDraft.DefaultTitle;

    /// <summary>リンク。空のままなら付けない</summary>
    [ObservableProperty]
    public partial string Link { get; set; } = "";

    /// <summary>行の下に出す文。無ければ空</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string Note { get; set; } = "";

    /// <summary>行の下に出す文があるか</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>開始時刻</summary>
    public TimeOnly StartTime => new(Math.Clamp(Hour, 0, 23), Math.Clamp(Minute, 0, 59));
}
