namespace MmmTool.WorkItems.Core;

/// <summary>作業の実績の累計の計算</summary>
/// <remarks>
/// 記録を保存するたびに、保存先 (SQLite・SQL Server)が、同じトランザクションの中で、その作業の記録をすべて読んでこの部品で足し直し、作業の行に書く
/// (差分は足さない。どこで何度実行しても結果は同じ。理由は docs/specs/work-items.md)。MmmBatch など、別の場所で作り直すときも、この部品を呼ぶ。
/// </remarks>
public static class WorkActualTotal
{
    /// <summary>記録の実績を足して、小数 2 桁に丸める</summary>
    /// <param name="hours">その作業の、論理削除されていない記録の実績 (全員の、すべての日)</param>
    /// <returns>実績の累計</returns>
    /// <remarks>10 進数で足す (浮動小数の誤差を、積み上げないため)。</remarks>
    public static double Calculate(IEnumerable<double> hours)
        => (double)Math.Round(hours.Sum(h => (decimal)h), 2, MidpointRounding.AwayFromZero);
}
