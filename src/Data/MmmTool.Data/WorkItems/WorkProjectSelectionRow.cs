using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkProjectSelection</c> (利用者が表示する案件に選んだ記録)の 1 行 (列と 1 対 1)。
/// </summary>
/// <remarks>
/// <c>ReminderState</c> と同じく、作成者・更新者は <see cref="UserId"/> と同じ人。選びを外したら <see cref="IsDeleted"/> にし、選び直したら同じ行を使い回す。
/// </remarks>
public sealed record WorkProjectSelectionRow
{
    /// <summary>主キー。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか (選びを外したか)</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (<see cref="UserId"/> と同じ人)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<see cref="UserId"/> と同じ人)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>選んだ人 (ユーザーの番号)</summary>
    public int UserId { get; init; }

    /// <summary>選んだ案件 (<c>WorkProject.Id</c>)</summary>
    public int ProjectId { get; init; }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>選びの記録</returns>
    public WorkProjectSelection ToWorkProjectSelection() => new(ProjectId, IsDeleted, UpdatedAt);
}
