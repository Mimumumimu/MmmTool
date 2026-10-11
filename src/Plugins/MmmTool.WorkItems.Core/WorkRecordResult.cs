namespace MmmTool.WorkItems.Core;

/// <summary>記録を保存した結果</summary>
/// <param name="Record">保存した記録 (番号・更新日時が決まったもの)。論理削除したときは、削除済みの記録。もともと無く、空のまま保存しなかったときは null</param>
/// <param name="ActualTotalHours">計算し直した、その作業の実績の累計</param>
public sealed record WorkRecordResult(WorkRecord? Record, double ActualTotalHours);
