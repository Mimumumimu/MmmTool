using MmmSdk.WinUI.Controls;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems;

/// <summary>表の行のうち、案件の行 (字下げの段 0)と、その下の行のかたまりを扱える行</summary>
public interface IProjectBlockRow : ITreeGridRow
{
    /// <summary>行の識別 (種類 + 番号)</summary>
    WorkKey Key { get; }

    /// <summary>案件の行か</summary>
    bool IsProject { get; }

    /// <summary>名前</summary>
    string Name { get; }
}

/// <summary>表示する案件の出し入れを、表の行のうち、その案件のかたまりだけで行う (作業管理の表とスケジュールで共有する)</summary>
/// <remarks>
/// 案件の中身は、ほかの案件に依存しない。そのため、外した案件は、行のかたまりを抜くだけ、加えた案件は、その案件の分だけ作った行を、名前の順の位置へ差し込めばよい。
/// 案件の並び (名前の順、同じなら番号の順)は、<see cref="WorkItemTree.CompareProjects"/> と同じ。
/// </remarks>
public static class ProjectBlockRows
{
    /// <summary>案件のかたまり (案件の行と、その下の行)を、表から抜く</summary>
    /// <typeparam name="TRow">行の型</typeparam>
    /// <param name="rows">表に渡している行の一覧</param>
    /// <param name="projectId">案件の番号</param>
    /// <returns>抜いた行 (上から順)。表に出ていない案件は null (反映できなかったことにして、全体を作り直すため)</returns>
    public static IReadOnlyList<TRow>? Remove<TRow>(IList<TRow> rows, int projectId)
        where TRow : class, IProjectBlockRow
    {
        var start = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsProject && rows[i].Key.Id == projectId)
            {
                start = i;
                break;
            }
        }
        if (start < 0)
        {
            return null;
        }

        // 次の案件の行の手前までが、この案件のかたまり (案件の行の字下げの段は 0)
        var end = start + 1;
        while (end < rows.Count && rows[end].Level > 0)
        {
            end++;
        }

        var removed = new List<TRow>(end - start);
        for (var i = start; i < end; i++)
        {
            removed.Add(rows[i]);
        }

        // 末尾から抜く (抜くたびに、後ろの行の位置が動かない)
        for (var i = end - 1; i >= start; i--)
        {
            rows.RemoveAt(i);
        }
        return removed;
    }

    /// <summary>案件のかたまりを、案件の名前の順の位置へ差し込む</summary>
    /// <typeparam name="TRow">行の型</typeparam>
    /// <param name="rows">表に渡している行の一覧</param>
    /// <param name="block">差し込む行 (先頭が案件の行。上から順)</param>
    public static void Insert<TRow>(IList<TRow> rows, IReadOnlyList<TRow> block)
        where TRow : class, IProjectBlockRow
    {
        var project = block[0];
        var index = rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsProject && WorkItemTree.CompareProjects(rows[i].Name, rows[i].Key.Id, project.Name, project.Key.Id) > 0)
            {
                index = i;
                break;
            }
        }

        foreach (var row in block)
        {
            rows.Insert(index++, row);
        }
    }
}
