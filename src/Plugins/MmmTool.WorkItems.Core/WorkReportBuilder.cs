using System.Globalization;
using System.Text;
using MmmTool.CodeMaster.Core;

namespace MmmTool.WorkItems.Core;

/// <summary>作業報告 (実績と予定のテキスト)を組み立てる</summary>
/// <remarks>
/// UI と保存先に依存しない整形。形と規則は docs/specs/work-report.md。
/// 実績は、期間 (実績の開始日から報告日の前日まで)に工数が記入された作業か、完了にした作業で、実績に含めない印が無い作業。予定は、状態が作業予定・作業中で、予定に含めない印が無い作業。
/// 並びは画面に表示している順 (案件は名前順、グループと作業は同じ親の下の並び順)。画面の表示の設定には従わない。
/// </remarks>
public static class WorkReportBuilder
{
    /// <summary>1 段の字下げ (全角スペース)</summary>
    private const string Indent = "　";

    /// <summary>行の区切り</summary>
    private const string NewLine = "\r\n";

    /// <summary>実績・予定が 1 件も無いときの文</summary>
    public const string EmptyText = "なし";

    /// <summary>作業報告のテキストを組み立てる</summary>
    /// <param name="data">読んだ内容</param>
    /// <param name="from">実績の開始日 (実績を拾い始める日)</param>
    /// <param name="reportDate">報告日 (予定の日。実績は、この日より前だけ)</param>
    /// <param name="showHours">実績の各作業に、期間内の工数の合計を添えるか</param>
    /// <param name="showProgress">実績と予定の各作業に、進捗度を添えるか</param>
    /// <returns>テキスト (行の区切りは CRLF。末尾に改行は付けない)</returns>
    /// <remarks>
    /// 日報メモ (<see cref="WorkDayNote"/>)は、報告日の分だけを予定の末尾に、書いたとおり出す。実績には出さない。空 (空白だけも含む)のメモは、無いものとして扱う。
    /// </remarks>
    public static string Build(WorkReportData data, DateOnly from, DateOnly reportDate, bool showHours, bool showProgress)
    {
        ArgumentNullException.ThrowIfNull(data);
        var codes = new CodeBook(data.Content.Codes);
        var projects = data.Content.Projects.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);
        var groups = ValidGroups(data.Content.Groups, projects);
        var items = data.Content.Items
            .Where(i => !i.IsDeleted && projects.ContainsKey(i.ProjectId) && (i.GroupId == 0 || groups.ContainsKey(i.GroupId)))
            .ToDictionary(i => i.Id);

        var records = data.Records
            .Where(r => !r.IsDeleted && items.ContainsKey(r.WorkItemId))
            .GroupBy(r => r.WorkItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Date).ThenBy(r => r.Id).ToList());

        // 実績: 期間内に、工数が 0 より大きい記録があるか、完了にした作業。備考は、共通の備考 → 期間内の備考 (日)の順。実績に含めない印のある作業は除く
        var done = new List<ReportEntry>();
        foreach (var item in items.Values.Where(i => i.ReportExclusion != WorkReportExclusion.PlanAndActual))
        {
            var inRange = records.TryGetValue(item.Id, out var itemRecords)
                ? itemRecords.Where(r => r.Date >= from && r.Date < reportDate).ToList()
                : [];
            var closedInRange = item.IsFinished && item.CompletedDate is { } completed && completed >= from && completed < reportDate;
            if (!closedInRange && !inRange.Any(r => r.Hours > 0))
            {
                continue;
            }
            var hours = WorkActualTotal.Calculate(inRange.Select(r => r.Hours));
            done.Add(new ReportEntry(item, Distinct([.. Lines(item.Note), .. inRange.SelectMany(r => Lines(r.Note))]), showHours ? hours : null));
        }

        // 予定: 状態が作業予定・作業中で、予定に含めない印が無い作業。共通の備考は、実績に出した作業でも、予定に出す
        var planned = new List<ReportEntry>();
        foreach (var item in items.Values.Where(i => i.ReportExclusion == WorkReportExclusion.None && i.Status is WorkItemStatus.Planned or WorkItemStatus.InProgress))
        {
            IEnumerable<string> todayNotes = records.TryGetValue(item.Id, out var itemRecords)
                ? itemRecords.Where(r => r.Date == reportDate).SelectMany(r => Lines(r.Note))
                : [];
            planned.Add(new ReportEntry(item, Distinct([.. Lines(item.Note), .. todayNotes]), null));
        }

        // 日報メモ: 予定の末尾に、報告日の分だけ出す (実績には出さない)
        var plannedNotes = data.DayNotes.Where(n => n.HasText && n.Date == reportDate).ToList();

        var builder = new StringBuilder();
        builder.Append("■実績").Append(NewLine);
        AppendSection(builder, done, projects, groups, codes, showProgress, []);
        builder.Append(NewLine).Append("■予定").Append(NewLine);
        AppendSection(builder, planned, projects, groups, codes, showProgress, Indented(plannedNotes.SelectMany(n => ManualLines(n.Note)), 1));
        return builder.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>行を、指定の段数だけ字下げする (空の行は、そのまま)</summary>
    /// <param name="lines">行</param>
    /// <param name="depth">字下げの段数 (全角スペース 1 つが 1 段)</param>
    /// <returns>字下げした行</returns>
    private static List<string> Indented(IEnumerable<string> lines, int depth)
    {
        var indent = string.Concat(Enumerable.Repeat(Indent, depth));
        return [.. lines.Select(l => l.Length == 0 ? l : indent + l)];
    }

    /// <summary>案件・親のグループをたどれるグループだけを返す (削除された案件・グループの中のグループを外す)</summary>
    /// <param name="source">読んだグループ</param>
    /// <param name="projects">削除されていない案件</param>
    /// <returns>グループの番号をキーにした、残したグループ (論理削除済みは含まない)</returns>
    private static Dictionary<int, WorkGroup> ValidGroups(IEnumerable<WorkGroup> source, Dictionary<int, WorkProject> projects)
    {
        var valid = source.Where(g => !g.IsDeleted).ToDictionary(g => g.Id);
        bool pruned;
        do
        {
            pruned = false;
            foreach (var group in valid.Values.Where(g => !projects.ContainsKey(g.ProjectId) || (g.ParentGroupId != 0 && !valid.ContainsKey(g.ParentGroupId))).ToList())
            {
                valid.Remove(group.Id);
                pruned = true;
            }
        }
        while (pruned);
        return valid;
    }

    /// <summary>備考を、空でない行に分ける</summary>
    /// <param name="note">備考</param>
    /// <returns>行 (行末の空白を除く。行頭の空白は、書いた人の字下げなので残す。空白だけの行は含まない)</returns>
    /// <remarks>改行は、CRLF・CR・LF のどれでも区切る (WinUI の複数行の入力欄は、改行を CR 1 文字で持つことがある)。</remarks>
    private static IEnumerable<string> Lines(string note)
        => note.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Select(l => l.TrimEnd()).Where(l => l.Length > 0);

    /// <summary>同じ行を 1 つにする (最初に出たものを残す)</summary>
    /// <param name="lines">行</param>
    /// <returns>重なりを除いた行</returns>
    private static List<string> Distinct(IEnumerable<string> lines) => [.. lines.Distinct(StringComparer.Ordinal)];

    /// <summary>実績または予定の 1 区画を書き出す</summary>
    /// <param name="builder">書き出し先</param>
    /// <param name="entries">区画に出す作業</param>
    /// <param name="projects">案件</param>
    /// <param name="groups">グループ</param>
    /// <param name="codes">コードブック (状態・進捗度の名前を引く)</param>
    /// <param name="showProgress">各作業に、進捗度を添えるか</param>
    /// <param name="extra">区画の末尾に足す日報メモの行 (字下げ済み。作業があるときは、空行を 1 つ挟む)</param>
    private static void AppendSection(StringBuilder builder, List<ReportEntry> entries, Dictionary<int, WorkProject> projects, Dictionary<int, WorkGroup> groups, CodeBook codes, bool showProgress, List<string> extra)
    {
        if (entries.Count == 0)
        {
            // 日報メモだけがあるときは、「なし」を出さない
            if (extra.Count == 0)
            {
                builder.Append(EmptyText).Append(NewLine);
            }
            AppendExtra(builder, extra);
            return;
        }

        // 出す作業と、その祖先だけで木を作る
        var roots = new Dictionary<int, Node>();
        var groupNodes = new Dictionary<int, Node>();
        foreach (var entry in entries)
        {
            var parent = EnsureParent(entry.Item.ProjectId, entry.Item.GroupId, projects, groups, roots, groupNodes);
            parent.Children.Add(new Node(NodeKind.Work, entry.Item.Id, entry.Item.Name, entry.Item.SortOrder) { Entry = entry });
        }

        foreach (var root in roots.Values.OrderBy(n => n.Name, StringComparer.CurrentCulture).ThenBy(n => n.Id))
        {
            builder.Append('【').Append(root.Name).Append('】').Append(NewLine);
            AppendChildren(builder, root, 1, codes, showProgress);
        }

        // 日報メモは、作業の後ろに、空行を 1 つ挟んで出す
        if (extra.Count > 0)
        {
            builder.Append(NewLine);
        }
        AppendExtra(builder, extra);
    }

    /// <summary>日報メモを、行に分ける (書いたとおり。前後の空行と、行末の空白だけを除く)</summary>
    /// <param name="text">日報メモ</param>
    /// <returns>行 (字下げは残す。途中の空行も残す)</returns>
    /// <remarks>改行は、CRLF・CR・LF のどれでも区切る。</remarks>
    private static List<string> ManualLines(string text)
    {
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        return lines;
    }

    /// <summary>日報メモの行を、区画の末尾に書き出す</summary>
    /// <param name="builder">書き出し先</param>
    /// <param name="extra">日報メモの行</param>
    private static void AppendExtra(StringBuilder builder, List<string> extra)
    {
        foreach (var line in extra)
        {
            builder.Append(line).Append(NewLine);
        }
    }

    /// <summary>作業の親 (案件の行か、グループの行)を、無ければ作って返す</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="groupId">グループの番号 (0 は案件の直下)</param>
    /// <param name="projects">案件</param>
    /// <param name="groups">グループ</param>
    /// <param name="roots">案件の行</param>
    /// <param name="groupNodes">グループの行</param>
    /// <returns>親の行</returns>
    private static Node EnsureParent(int projectId, int groupId, Dictionary<int, WorkProject> projects, Dictionary<int, WorkGroup> groups, Dictionary<int, Node> roots, Dictionary<int, Node> groupNodes)
    {
        if (groupId == 0)
        {
            if (!roots.TryGetValue(projectId, out var root))
            {
                root = new Node(NodeKind.Project, projectId, projects[projectId].Name, 0);
                roots.Add(projectId, root);
            }
            return root;
        }

        if (groupNodes.TryGetValue(groupId, out var existing))
        {
            return existing;
        }
        var group = groups[groupId];
        var node = new Node(NodeKind.Group, group.Id, group.Name, group.SortOrder);
        groupNodes.Add(groupId, node);
        EnsureParent(group.ProjectId, group.ParentGroupId, projects, groups, roots, groupNodes).Children.Add(node);
        return node;
    }

    /// <summary>行の子を、並び順に書き出す (子がグループ 1 つだけのグループは、1 行につなぐ)</summary>
    /// <param name="builder">書き出し先</param>
    /// <param name="parent">親の行</param>
    /// <param name="depth">子の字下げの段数</param>
    /// <param name="codes">コードブック</param>
    /// <param name="showProgress">各作業に、進捗度を添えるか</param>
    private static void AppendChildren(StringBuilder builder, Node parent, int depth, CodeBook codes, bool showProgress)
    {
        foreach (var child in parent.Children.OrderBy(c => c.SortOrder).ThenBy(c => c.Kind).ThenBy(c => c.Id))
        {
            var indent = string.Concat(Enumerable.Repeat(Indent, depth));
            if (child.Kind == NodeKind.Work)
            {
                AppendWork(builder, child.Entry!, indent, depth, codes, showProgress);
                continue;
            }

            var names = new List<string> { child.Name };
            var current = child;
            while (current.Children.Count == 1 && current.Children[0].Kind == NodeKind.Group)
            {
                current = current.Children[0];
                names.Add(current.Name);
            }
            builder.Append(indent).Append('[').Append(string.Join(" > ", names)).Append(']').Append(NewLine);
            AppendChildren(builder, current, depth + 1, codes, showProgress);
        }
    }

    /// <summary>作業の行と備考を書き出す</summary>
    /// <param name="builder">書き出し先</param>
    /// <param name="entry">作業と、出す備考・工数</param>
    /// <param name="indent">作業の行の字下げ</param>
    /// <param name="depth">作業の行の字下げの段数</param>
    /// <param name="codes">コードブック</param>
    /// <param name="showProgress">進捗度 (<c>70% (レビュー中)</c> の形)を添えるか</param>
    private static void AppendWork(StringBuilder builder, ReportEntry entry, string indent, int depth, CodeBook codes, bool showProgress)
    {
        builder.Append(indent).Append(entry.Item.Name).Append('：').Append(WorkCodes.Display(codes, WorkCodes.StatusCategory, (int)entry.Item.Status));
        if (showProgress)
        {
            builder.Append(' ').Append(WorkCodes.Display(codes, WorkCodes.ProgressCategory, entry.Item.ProgressCodeNo));
        }
        if (entry.Hours is { } hours)
        {
            builder.Append(" [").Append(hours.ToString("0.##", CultureInfo.InvariantCulture)).Append("h]");
        }
        if (entry.Notes.Count == 1)
        {
            // 1 行だけのときは、行頭の字下げを除く (「 - 」のすぐ後ろに続けるため)
            builder.Append(" - ").Append(entry.Notes[0].TrimStart()).Append(NewLine);
            return;
        }
        builder.Append(NewLine);
        var noteIndent = string.Concat(Enumerable.Repeat(Indent, depth + 1));
        foreach (var note in entry.Notes)
        {
            builder.Append(noteIndent).Append(note).Append(NewLine);
        }
    }

    /// <summary>区画に出す作業 1 件</summary>
    /// <param name="Item">作業</param>
    /// <param name="Notes">出す備考 (行ごと。重なりを除いた)</param>
    /// <param name="Hours">添える工数 (添えないなら null)</param>
    private sealed record ReportEntry(WorkItem Item, List<string> Notes, double? Hours);

    /// <summary>木の行の種類 (並びの決めに使う。同じ並び順なら、グループが作業より先)</summary>
    private enum NodeKind
    {
        /// <summary>案件</summary>
        Project = 0,

        /// <summary>グループ</summary>
        Group = 1,

        /// <summary>作業</summary>
        Work = 2,
    }

    /// <summary>出力用の木の行</summary>
    /// <param name="Kind">種類</param>
    /// <param name="Id">番号</param>
    /// <param name="Name">名前</param>
    /// <param name="SortOrder">同じ親の下の並び順</param>
    private sealed record Node(NodeKind Kind, int Id, string Name, int SortOrder)
    {
        /// <summary>子の行</summary>
        public List<Node> Children { get; } = [];

        /// <summary>作業の行のときの、出す内容</summary>
        public ReportEntry? Entry { get; init; }
    }
}
