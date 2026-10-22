namespace MmmTool.Notes.Core;

/// <summary>
/// ノートの入力内容 (新規の登録・編集で保存する値)。
/// </summary>
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

    /// <summary>終了日 (含む)。無期限は <see cref="DateOnly.MaxValue"/></summary>
    public DateOnly EndDate { get; init; } = DateOnly.MaxValue;

    /// <summary>実施日。未設定は <see cref="DateOnly.MaxValue"/></summary>
    public DateOnly EventDate { get; init; } = DateOnly.MaxValue;

    /// <summary>実施日が設定されているか</summary>
    public bool HasEventDate => EventDate != DateOnly.MaxValue;

    /// <summary>実施日が、開始日から終了日の外にあるか (未設定は、外ではない)</summary>
    public bool IsEventDateOutOfPeriod => HasEventDate && (EventDate < StartDate || EventDate > EndDate);

    /// <summary>保存できる内容かを調べる</summary>
    /// <returns>保存できるなら null。できないときは、画面に出せる理由</returns>
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
        return null;
    }

    /// <summary>前後の空白を取り除いた、保存する内容にする</summary>
    /// <returns>タイトルの前後の空白を取り除いた内容 (本文は、書いたとおりのまま)</returns>
    public NoteInput Trimmed() => this with { Title = Title.Trim() };
}
