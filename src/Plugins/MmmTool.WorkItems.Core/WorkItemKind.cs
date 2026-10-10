using System.Text.Json.Serialization;

namespace MmmTool.WorkItems.Core;

/// <summary>行の種類</summary>
/// <remarks>作ったときに決め、あとから変えない (理由は docs/specs/work-items.md)。</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<WorkItemKind>))]
public enum WorkItemKind
{
    /// <summary>グループ (子を持つ行。名前と備考だけ入力し、日付・進捗度・予定時間・実績は子の作業から計算する)</summary>
    Group = 1,

    /// <summary>作業 (入力する行。子を持たない)</summary>
    Work = 2,
}
