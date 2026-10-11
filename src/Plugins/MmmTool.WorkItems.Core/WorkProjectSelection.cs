namespace MmmTool.WorkItems.Core;

/// <summary>利用者が、表示する案件に選んだ記録 (自分の分)</summary>
/// <param name="ProjectId">選んだ案件の番号</param>
/// <param name="IsDeleted">選びを外したか (外した行は論理削除。選び直したら同じ行を使い回す)</param>
/// <param name="UpdatedAt">更新日時 (保存先の時計。別の PC での選びの変更を、差分で知るために使う)</param>
public sealed record WorkProjectSelection(int ProjectId, bool IsDeleted, DateTimeOffset UpdatedAt);
