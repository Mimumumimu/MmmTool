using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkGroup</c> の 1 行 (列と 1 対 1)。アプリの型 <see cref="WorkGroup"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>変換は <see cref="FromWorkGroup"/> と <see cref="ToWorkGroup"/>。作成・更新の日時と作成者・更新者は、Repository が入れる。</remarks>
public sealed record WorkGroupRow
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

    /// <summary>属する案件 (<c>WorkProject.Id</c>)</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループ (<c>WorkGroup.Id</c>。0 は案件の直下)</summary>
    public int ParentGroupId { get; init; }

    /// <summary>兄弟の中の並び (グループと作業にまたがる 1 つの数)</summary>
    public int SortOrder { get; init; }

    /// <summary>名前</summary>
    public string Name { get; init; } = "";

    /// <summary>備考 (無いときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="group">グループ</param>
    /// <returns>DB の行。作成・更新の日時と作成者・更新者は決めない (Repository が入れる)</returns>
    /// <exception cref="ArgumentException">名前が空・長すぎる、備考が長すぎる、番号が負。</exception>
    public static WorkGroupRow FromWorkGroup(WorkGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (group.ProjectId <= 0 || group.ParentGroupId < 0 || group.SortOrder < 0)
        {
            throw new ArgumentException("グループの案件・親・並びの番号が正しくありません。", nameof(group));
        }
        return new WorkGroupRow
        {
            Id = group.Id,
            IsDeleted = group.IsDeleted,
            ProjectId = group.ProjectId,
            ParentGroupId = group.ParentGroupId,
            SortOrder = group.SortOrder,
            Name = WorkRowValues.CheckText(group.Name, "グループの名前", WorkRowValues.NameMax, required: true),
            Note = WorkRowValues.CheckText(group.Note, "グループの備考", WorkRowValues.NoteMax),
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>グループ</returns>
    public WorkGroup ToWorkGroup() => new()
    {
        Id = Id,
        ProjectId = ProjectId,
        ParentGroupId = ParentGroupId,
        SortOrder = SortOrder,
        Name = Name,
        Note = Note,
        IsDeleted = IsDeleted,
        UpdatedAt = UpdatedAt,
    };
}
