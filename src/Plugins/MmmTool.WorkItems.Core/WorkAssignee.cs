namespace MmmTool.WorkItems.Core;

/// <summary>担当者にできる人 (ユーザーの 1 人)</summary>
/// <param name="UserId">ユーザーの番号</param>
/// <param name="Name">表示名</param>
/// <param name="IsSelectable">担当に選べるか (削除されておらず、使える期間の中。担当に付いているだけで、名前を引くための人は false)</param>
public sealed record WorkAssignee(int UserId, string Name, bool IsSelectable);
