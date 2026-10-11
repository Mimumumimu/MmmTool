using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkProject</c> の 1 行 (列と 1 対 1)。アプリの型 <see cref="WorkProject"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>変換は <see cref="FromWorkProject"/> と <see cref="ToWorkProject"/>。作成・更新の日時と作成者・更新者は、アプリの型の更新日時を除いて、Repository が入れる。</remarks>
public sealed record WorkProjectRow
{
    /// <summary>主キー。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (ユーザーの番号)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>名前</summary>
    public string Name { get; init; } = "";

    /// <summary>備考 (無いときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="project">案件</param>
    /// <returns>DB の行。作成・更新の日時と作成者・更新者は決めない (Repository が入れる)</returns>
    /// <exception cref="ArgumentException">名前が空・長すぎる、備考が長すぎる。</exception>
    public static WorkProjectRow FromWorkProject(WorkProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new WorkProjectRow
        {
            Id = project.Id,
            IsDeleted = project.IsDeleted,
            Name = WorkRowValues.CheckText(project.Name, "案件の名前", WorkRowValues.NameMax, required: true),
            Note = WorkRowValues.CheckText(project.Note, "案件の備考", WorkRowValues.NoteMax),
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>案件</returns>
    public WorkProject ToWorkProject() => new()
    {
        Id = Id,
        Name = Name,
        Note = Note,
        IsDeleted = IsDeleted,
        UpdatedAt = UpdatedAt,
    };
}
