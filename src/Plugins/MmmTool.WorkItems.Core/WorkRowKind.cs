namespace MmmTool.WorkItems.Core;

/// <summary>表の行の種類</summary>
/// <remarks>
/// 作ったときに決め、あとから変えない。
/// 理由は docs/specs/work-items.md にある。
/// 案件・グループ・作業は別の表に持つので、番号は種類ごとに別の連番。
/// </remarks>
public enum WorkRowKind
{
    /// <summary>案件で、案件マスターの 1 件</summary>
    /// <remarks>一番上の行</remarks>
    Project = 1,

    /// <summary>グループで、案件やグループの下に置く子を持つ行</summary>
    /// <remarks>名前と備考だけ入力し、日付・進捗度・予定時間・実績は子の作業から計算する。</remarks>
    Group = 2,

    /// <summary>作業で、入力する行</summary>
    /// <remarks>子を持たない</remarks>
    Work = 3,
}
