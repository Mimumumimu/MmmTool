namespace MmmTool.Core.Reminders;

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
