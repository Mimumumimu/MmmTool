using MmmTool.Reminders.Core;

namespace MmmTool.Data.Reminders;

/// <summary>
/// DB の <c>dbo.ReminderState</c> の 1 行で、列と 1 対 1 に対応する。アプリの型 <see cref="ReminderState"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 状態は人 (<see cref="UserId"/>)ごと・リマインダー (<see cref="ReminderId"/>)ごとに 1 件。未対応 (<see cref="ReminderStatus.None"/>)に戻したときも、行を消さずに、
/// <see cref="Status"/> を 0 にして残す。<see cref="Date"/> が今日でない行は、未対応として扱う。<see cref="ReminderState"/> との変換は
/// <see cref="FromReminderState"/> と <see cref="ToReminderState"/>。
/// </remarks>
public sealed record ReminderStateRow
{
    /// <summary>主キーで、<see cref="ReminderState.Seq"/> にあたる。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    /// <remarks>常に false。共通ヘッダーをそろえるための列で、未対応に戻したときも、行は消さず、削除もせず、<see cref="Status"/> を 0 にする。</remarks>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    /// <remarks>DB の既定値で入る。<see cref="FromReminderState"/> では決めない。</remarks>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (<c>AppUser.Id</c>。<see cref="UserId"/> と同じ人)</summary>
    /// <remarks>アプリの型 (<see cref="ReminderState"/>)に無いので、<see cref="FromReminderState"/> では決めない。Repository が入れる。</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    /// <remarks>更新の SQL の中で、DB サーバーの時計を使って書く。<see cref="FromReminderState"/> では決めない。</remarks>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<c>AppUser.Id</c>。<see cref="UserId"/> と同じ人)</summary>
    /// <remarks>アプリの型に無いので、<see cref="FromReminderState"/> では決めない。Repository が入れる。</remarks>
    public int UpdatedByUserId { get; init; }

    /// <summary>対応するリマインダー (<c>Reminder.Id</c>。<see cref="ReminderState.BaseNo"/>)</summary>
    public int ReminderId { get; init; }

    /// <summary>状態を持つ人 (<c>AppUser.Id</c>)</summary>
    /// <remarks>アプリの型に無いので、<see cref="FromReminderState"/> では決めない。Repository が入れる。</remarks>
    public int UserId { get; init; }

    /// <summary>どの日の状態か</summary>
    public DateOnly Date { get; init; }

    /// <summary>対応状態 (未対応 = 0・完了 = 1・スヌーズ = 2)</summary>
    public byte Status { get; init; }

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="state">対応状態</param>
    /// <returns>DB の行。作成・更新の日時と、作成者・更新者・人は決めない (Repository が入れる)</returns>
    /// <exception cref="ArgumentException">
    /// 行にできない値。対応状態の値が範囲外か、日付が正しくない。手で直した JSON の誤りなど。
    /// </exception>
    public static ReminderStateRow FromReminderState(ReminderState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status is not (ReminderStatus.None or ReminderStatus.Done or ReminderStatus.Snooze))
        {
            throw new ArgumentException($"対応状態 {(int)state.Status} は、行にできません。", nameof(state));
        }

        var date = ReminderDates.ToDate(state.Date)
            ?? throw new ArgumentException($"日付 {state.Date} は、日付として正しくありません。", nameof(state));

        return new ReminderStateRow
        {
            Id = state.Seq,
            ReminderId = state.BaseNo,
            Date = date,
            Status = (byte)state.Status,
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>対応状態</returns>
    public ReminderState ToReminderState() => new()
    {
        Seq = Id,
        BaseNo = ReminderId,
        Date = ReminderDates.ToDateValue(Date),
        Status = (ReminderStatus)Status,
    };
}
