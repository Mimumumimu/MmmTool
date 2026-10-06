namespace MmmTool.Reminders.Core;

/// <summary>リマインダー 1 件 (本体)</summary>
/// <remarks>
/// 日付指定 (<see cref="Date"/> に yyyyMMdd)と曜日指定 (<see cref="Date"/> に <see cref="ReminderDates.NoDate"/>、
/// <see cref="Weekdays"/> に曜日)の 2 通りがある。対応状態は <see cref="ReminderState"/> に別に持ち、<see cref="No"/> で紐付ける。
/// 値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る (<c>init</c>)。
/// </remarks>
public sealed record Reminder
{
    /// <summary>番号</summary>
    /// <remarks>登録順の一意の連番 (ID)で、<see cref="ReminderState.BaseNo"/> が指すキー。0 は未採番 (新規)。</remarks>
    public int No { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>発動日 (yyyyMMdd の整数)</summary>
    /// <remarks>曜日指定のときは <see cref="ReminderDates.NoDate"/>。</remarks>
    public int Date { get; init; }

    /// <summary>発動時刻 (HHmm の整数)</summary>
    public int Time { get; init; }

    /// <summary>曜日指定</summary>
    /// <remarks>日付指定のときは <see cref="Weekdays.None"/>。曜日指定で <see cref="Weekdays.None"/> なら毎日。</remarks>
    public Weekdays Weekdays { get; init; }

    /// <summary>件名</summary>
    public string Title { get; init; } = "";

    /// <summary>備考</summary>
    public string? Note { get; init; }

    /// <summary>リンク (URL・ファイル・フォルダのパス)</summary>
    public string? Link { get; init; }

    /// <summary>通知のときに読み上げるか</summary>
    /// <remarks>既存のデータ (この項目が無い JSON)は false (読み上げない)で読み込む。</remarks>
    public bool IsSpeak { get; init; }
}
