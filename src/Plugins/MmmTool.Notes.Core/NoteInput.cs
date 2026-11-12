namespace MmmTool.Notes.Core;

/// <summary>ノートの入力内容で新規の登録・編集で保存する値</summary>
/// <remarks>
/// 期間・実施日・ほかの実施日の組み合わせは <see cref="Validate"/> が調べる。
/// ほかの実施日は <c>NoteAttribute</c> 表に、1 日ごとに 1 行で保存する。
/// </remarks>
public sealed record NoteInput
{
    /// <summary>タイトル</summary>
    public string Title { get; init; } = "";

    /// <summary>本文</summary>
    public string Body { get; init; } = "";

    /// <summary>重要度</summary>
    public NotePriority Priority { get; init; } = NotePriority.Normal;

    /// <summary>書いた人だけに見えるか</summary>
    public bool IsPrivate { get; init; }

    /// <summary>開始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>終了日で、含む</summary>
    /// <remarks>無期限は DateOnly.MaxValue</remarks>
    public DateOnly EndDate { get; init; } = DateOnly.MaxValue;

    /// <summary>実施日</summary>
    /// <remarks>未設定は DateOnly.MaxValue</remarks>
    public DateOnly EventDate { get; init; } = DateOnly.MaxValue;

    /// <summary>ほかの実施日</summary>
    /// <remarks>
    /// 保存するときに日付の順にする
    /// 実施日と同じ日・同じ日の重なりは入れられない
    /// 並びは問わない
    /// </remarks>
    public IReadOnlyList<DateOnly> OtherEventDates { get; init; } = [];

    /// <summary>実施日が設定されているか</summary>
    public bool HasEventDate => EventDate != DateOnly.MaxValue;

    /// <summary>実施日が開始日から終了日の外にあるか</summary>
    /// <remarks>未設定は外ではない</remarks>
    public bool IsEventDateOutOfPeriod => HasEventDate && (EventDate < StartDate || EventDate > EndDate);

    /// <summary>ほかの実施日のうち開始日から終了日の外にあるものがあるか</summary>
    public bool IsOtherEventDateOutOfPeriod => OtherEventDates.Any(IsOutOfPeriod);

    /// <summary>指定の日が開始日から終了日の外にあるか</summary>
    /// <param name="day">調べる日</param>
    /// <returns>外なら true</returns>
    public bool IsOutOfPeriod(DateOnly day) => day < StartDate || day > EndDate;

    /// <summary>保存できる内容かを調べる</summary>
    /// <returns>
    /// 保存できるなら null
    /// できないときは画面に出せる理由
    /// </returns>
    public string? Validate()
    {
        var title = Title.Trim();
        if (title.Length == 0)
        {
            return "タイトルを入力してください。";
        }
        if (title.Length > NoteLimits.TitleMaxLength)
        {
            return $"タイトルは {NoteLimits.TitleMaxLength} 文字までです。";
        }
        if (!Enum.IsDefined(Priority))
        {
            return "重要度が正しくありません。";
        }
        if (EndDate < StartDate)
        {
            return "終了日は、開始日以降にしてください。";
        }
        if (IsEventDateOutOfPeriod)
        {
            return "実施日は、開始日から終了日の間にしてください。";
        }
        if (OtherEventDates.Count > NoteLimits.OtherEventDatesMaxCount)
        {
            return $"ほかの実施日は {NoteLimits.OtherEventDatesMaxCount} 日までです。";
        }
        if (OtherEventDates.Contains(DateOnly.MaxValue))
        {
            return "ほかの実施日が正しくありません。";
        }
        if (IsOtherEventDateOutOfPeriod)
        {
            return "ほかの実施日は、開始日から終了日の間にしてください。";
        }
        if (OtherEventDates.Distinct().Count() != OtherEventDates.Count || (HasEventDate && OtherEventDates.Contains(EventDate)))
        {
            return "実施日が重複しています";
        }
        return null;
    }

    /// <summary>前後の空白を取り除き、ほかの実施日を日付の順にした、保存する内容にする</summary>
    /// <returns>タイトルの前後の空白を取り除いた内容 (本文は書いたとおりのまま)</returns>
    public NoteInput Trimmed() => this with { Title = Title.Trim(), OtherEventDates = [.. OtherEventDates.Order()] };
}
