namespace MmmTool.Notes.Core;

/// <summary>ノートの入力内容で新規の登録・編集で保存する値</summary>
/// <remarks>
/// 期間・実施日・ほかの実施日・日程の組み合わせは <see cref="Validate"/> が調べる。
/// ほかの実施日と日程は <c>NoteAttribute</c> 表に、種類・枝番・項目ごとに 1 行で保存する。
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

    /// <summary>ノートの種類</summary>
    /// <remarks>作ったあとに変えられない</remarks>
    public NoteKind Kind { get; init; } = NoteKind.Normal;

    /// <summary>日程</summary>
    /// <remarks>
    /// 種類が日程のときだけ使う
    /// 保存するとき日付と開始の順にする
    /// 同じ日に何件でも入れられる
    /// 並びは問わない
    /// </remarks>
    public IReadOnlyList<ScheduleItem> ScheduleItems { get; init; } = [];

    /// <summary>ほかの実施日</summary>
    /// <remarks>
    /// 種類が通常のときだけ使う。
    /// 保存するとき日付の順にして、同じ日は 1 つにまとめる。
    /// 並びは問わない。
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
        if (!Enum.IsDefined(Kind))
        {
            return "種類が正しくありません。";
        }
        return Kind == NoteKind.Schedule ? ScheduleError() : OtherEventDatesError();
    }

    /// <summary>保存する内容にする</summary>
    /// <returns>
    /// タイトルの前後の空白を取り除いた内容
    /// 本文は書いたとおりのまま
    /// </returns>
    /// <remarks>
    /// 種類が日程なら、日程の内容と備考の空白も取り除いて、日付と開始の順に並べ、ほかの実施日は空にする。
    /// 通常なら、ほかの実施日を日付の順にして同じ日をまとめ、日程は空にする。
    /// </remarks>
    public NoteInput Trimmed() => Kind == NoteKind.Schedule
        ? this with
        {
            Title = Title.Trim(),
            OtherEventDates = [],
            ScheduleItems = ScheduleItem.Sort(ScheduleItems.Select(item => item with { Content = item.Content.Trim(), Remark = item.Remark.Trim() })),
        }
        : this with { Title = Title.Trim(), ScheduleItems = [], OtherEventDates = [.. OtherEventDates.Distinct().Order()] };

    /// <summary>ほかの実施日の検査</summary>
    /// <returns>
    /// 正しいなら null
    /// 正しくないときは画面に出せる理由
    /// </returns>
    private string? OtherEventDatesError()
    {
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
        return null;
    }

    /// <summary>日程の検査</summary>
    /// <returns>
    /// 正しいなら null
    /// 正しくないときは画面に出せる理由
    /// </returns>
    private string? ScheduleError()
    {
        if (ScheduleItems.Count > NoteLimits.ScheduleItemsMaxCount)
        {
            return $"日程は {NoteLimits.ScheduleItemsMaxCount} 件までです。";
        }
        foreach (var item in ScheduleItems)
        {
            if (item.Date == DateOnly.MaxValue)
            {
                return "日程の日付が正しくありません。";
            }
            if (IsOutOfPeriod(item.Date))
            {
                return "日程の日付は、開始日から終了日の間にしてください。";
            }
            if (item.End.HasValue && !item.Start.HasValue)
            {
                return "終了を入れるときは、開始も入れてください。";
            }
            if (item.Start.HasValue && item.End.HasValue && item.End.Value < item.Start.Value)
            {
                return "終了は、開始以降にしてください。";
            }
            if (item.Content.Trim().Length > NoteLimits.ScheduleTextMaxLength)
            {
                return $"内容は {NoteLimits.ScheduleTextMaxLength} 文字までです。";
            }
            if (item.Remark.Trim().Length > NoteLimits.ScheduleTextMaxLength)
            {
                return $"備考は {NoteLimits.ScheduleTextMaxLength} 文字までです。";
            }
        }
        return null;
    }
}
