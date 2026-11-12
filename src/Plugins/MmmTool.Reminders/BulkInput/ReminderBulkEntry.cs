using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.BulkInput;

/// <summary>まとめて追加の右の一覧の選んだ会議 1 件</summary>
/// <param name="slot">この会議を選んだ、時刻の表のマス。</param>
/// <remarks>時刻・件名・リンクを直せる</remarks>
public sealed partial class ReminderBulkEntry(ReminderBulkSlot slot) : ObservableObject
{
    /// <summary>この会議を選んだ時刻の表のマス</summary>
    /// <remarks>
    /// マスの選びを外すとこの会議も外れる
    /// 時刻を直しても結びつきは変わらない
    /// </remarks>
    public ReminderBulkSlot Slot { get; } = slot;

    /// <summary>開始時刻の時</summary>
    [ObservableProperty]
    public partial int Hour { get; set; } = slot.Time.Hour;

    /// <summary>開始時刻の分</summary>
    [ObservableProperty]
    public partial int Minute { get; set; } = slot.Time.Minute;

    /// <summary>件名</summary>
    /// <remarks>
    /// 行が増えたときは「会議」が入っている。
    /// 空にすると登録できない。
    /// 入れ直すまで、登録を押せない。
    /// </remarks>
    [ObservableProperty]
    public partial string Title { get; set; } = ReminderDraft.InitialTitle;

    /// <summary>リンク</summary>
    /// <remarks>空のままなら付けない</remarks>
    [ObservableProperty]
    public partial string Link { get; set; } = "";

    /// <summary>行の下に出す文</summary>
    /// <remarks>無ければ空</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string Note { get; set; } = "";

    /// <summary>行の下に出す文があるか</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>開始時刻</summary>
    public TimeOnly StartTime => new(Math.Clamp(Hour, 0, 23), Math.Clamp(Minute, 0, 59));
}
