namespace MmmTool.Core.Reminders;

/// <summary>リマインダー 1 件（本体）</summary>
/// <remarks>
/// 日付指定（<see cref="Date"/> に yyyyMMdd）と曜日指定（<see cref="Date"/> に <see cref="ReminderDates.NoDate"/>、
/// <see cref="Weekdays"/> に曜日）の 2 通りがある。対応状態は <see cref="ReminderState"/> に別に持ち、<see cref="No"/> で紐付ける。
/// </remarks>
public sealed record Reminder
{
    /// <summary>連番</summary>
    /// <remarks>登録順の一意の連番（ID）。0 は未採番（新規）。</remarks>
    public int Seq { get; set; }

    /// <summary>参照番号</summary>
    /// <remarks>状態と紐付けるキー。新規時は <see cref="Seq"/> と同じ値になる。</remarks>
    public int No { get; set; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; set; }

    /// <summary>発動日（yyyyMMdd の整数）</summary>
    /// <remarks>曜日指定のときは <see cref="ReminderDates.NoDate"/>。</remarks>
    public int Date { get; set; }

    /// <summary>発動時刻（HHmm の整数）</summary>
    public int Time { get; set; }

    /// <summary>曜日指定</summary>
    /// <remarks>日付指定のときは <see cref="Weekdays.None"/>。曜日指定で <see cref="Weekdays.None"/> なら毎日。</remarks>
    public Weekdays Weekdays { get; set; }

    /// <summary>件名</summary>
    public string Title { get; set; } = "";

    /// <summary>備考</summary>
    public string? Note { get; set; }

    /// <summary>リンク（URL・ファイル・フォルダのパス）</summary>
    public string? Link { get; set; }
}
