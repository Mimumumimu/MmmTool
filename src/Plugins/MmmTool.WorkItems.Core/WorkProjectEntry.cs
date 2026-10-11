namespace MmmTool.WorkItems.Core;

/// <summary>案件の一覧の 1 件 (案件を選ぶ画面と、案件の管理の画面で使う)</summary>
/// <param name="Id">案件の番号</param>
/// <param name="Name">名前</param>
/// <param name="IsSelected">自分が、表示する案件に選んでいるか</param>
/// <param name="IsFinished">完了済みか (作業が 1 件以上あり、未完了の作業が 1 件も無い。読むときに計算する)</param>
/// <param name="CompletedDate">完了日 (完了済みのとき、下の作業の完了日の最大。完了済みでなければ null)</param>
public sealed record WorkProjectEntry(int Id, string Name, bool IsSelected, bool IsFinished, DateOnly? CompletedDate);
