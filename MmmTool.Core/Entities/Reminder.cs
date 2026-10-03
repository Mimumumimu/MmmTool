namespace MmmTool.Core.Entities;

/// <summary>リマインダー 1 件（本体）</summary>
/// <remarks>
/// 日付指定（<see cref="Date"/> に yyyyMMdd）と曜日指定（<see cref="Date"/> に <see cref="Services.ReminderDates.NoDate"/>、
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
    /// <remarks>曜日指定のときは <see cref="Services.ReminderDates.NoDate"/>。</remarks>
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

/// <summary>リマインダーの対応状態 1 件</summary>
/// <remarks>1 つのリマインダー（<see cref="BaseNo"/>）につき 1 件だけ持ち、対象日・値を上書きしていく。</remarks>
public sealed record ReminderState
{
    /// <summary>連番</summary>
    public int Seq { get; set; }

    /// <summary>リマインダー本体の参照番号（<see cref="Reminder.No"/>）</summary>
    public int BaseNo { get; set; }

    /// <summary>対象日（yyyyMMdd の整数）</summary>
    /// <remarks>この日の状態であることを表す。別の日には <see cref="ReminderStatus.None"/> として扱う。</remarks>
    public int Date { get; set; }

    /// <summary>対応状態</summary>
    public ReminderStatus Status { get; set; }
}

/// <summary>曜日フラグ（月〜日のビット OR）</summary>
[Flags]
public enum Weekdays
{
    /// <summary>指定なし（日付指定）</summary>
    None = 0,
    /// <summary>月曜日</summary>
    Monday = 1,
    /// <summary>火曜日</summary>
    Tuesday = 2,
    /// <summary>水曜日</summary>
    Wednesday = 4,
    /// <summary>木曜日</summary>
    Thursday = 8,
    /// <summary>金曜日</summary>
    Friday = 16,
    /// <summary>土曜日</summary>
    Saturday = 32,
    /// <summary>日曜日</summary>
    Sunday = 64,
}

/// <summary>リマインダーの対応状態</summary>
public enum ReminderStatus
{
    /// <summary>未対応</summary>
    None = 0,
    /// <summary>完了</summary>
    Done = 1,
    /// <summary>スヌーズ</summary>
    Snooze = 2,
}
