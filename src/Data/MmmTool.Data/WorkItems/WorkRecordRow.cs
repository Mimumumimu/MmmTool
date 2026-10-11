using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkRecord</c> (日ごとの記録)の 1 行 (列と 1 対 1)。アプリの型 <see cref="WorkRecord"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 記録した人は <see cref="CreatedByUserId"/> (人ごとに 1 件)。変換は <see cref="FromWorkRecord"/> と <see cref="ToWorkRecord"/>。
/// 作成・更新の日時と更新者は、Repository が入れる。
/// </remarks>
public sealed record WorkRecordRow
{
    /// <summary>主キー (<c>bigint</c>)。0 は新規 (DB が採番する)</summary>
    public long Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 = 記録した人 (ユーザーの番号)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>属する案件 (<c>WorkProject.Id</c>。作業の案件)</summary>
    public int ProjectId { get; init; }

    /// <summary>作業 (<c>WorkItem.Id</c>)</summary>
    public int WorkItemId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>その日の実績 (時間)</summary>
    public decimal Hours { get; init; }

    /// <summary>その日の備考 (無いときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="record">記録</param>
    /// <returns>DB の行。作成・更新の日時と更新者は決めない (Repository が入れる)。作成者は記録した人</returns>
    /// <exception cref="ArgumentException">DB に入れられない値 (実績が 0 から 24 の外、備考が長すぎるなど)。</exception>
    public static WorkRecordRow FromWorkRecord(WorkRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.ProjectId <= 0 || record.WorkItemId <= 0 || record.UserId < 0)
        {
            throw new ArgumentException("記録の案件・作業・人の番号が正しくありません。", nameof(record));
        }
        return new WorkRecordRow
        {
            Id = record.Id,
            IsDeleted = record.IsDeleted,
            CreatedByUserId = record.UserId,
            ProjectId = record.ProjectId,
            WorkItemId = record.WorkItemId,
            Date = record.Date,
            Hours = WorkRowValues.FromHours(record.Hours, "実績", WorkItemLimits.DailyHoursMax),
            Note = WorkRowValues.CheckText(record.Note, "日の備考", WorkRowValues.NoteMax),
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>記録</returns>
    public WorkRecord ToWorkRecord() => new()
    {
        Id = Id,
        ProjectId = ProjectId,
        WorkItemId = WorkItemId,
        Date = Date,
        UserId = CreatedByUserId,
        Hours = WorkRowValues.ToHours(Hours),
        Note = Note,
        IsDeleted = IsDeleted,
        UpdatedAt = UpdatedAt,
    };
}
