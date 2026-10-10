using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Main;

/// <summary>表の選択欄 (優先度・状態)の選択肢の表示名</summary>
/// <remarks>選択肢の並びは Core の <see cref="WorkItemNames"/> と同じ。XAML から、静的なプロパティとして結び付ける。</remarks>
public static class WorkItemOptions
{
    /// <summary>優先度の選択肢 (高い順)</summary>
    public static IReadOnlyList<string> Priorities { get; } = [.. WorkItemNames.Priorities.Select(WorkItemNames.Of)];

    /// <summary>状態の選択肢 (進む順)</summary>
    public static IReadOnlyList<string> Statuses { get; } = [.. WorkItemNames.Statuses.Select(WorkItemNames.Of)];

    /// <summary>案件の名前の文字の大きさ (ほかは 14。一回り大きくして、グループと見分ける)</summary>
    public static double ProjectFontSize => 16;

    /// <summary>名前の列の文字の太さ (案件は太字、グループはやや太字)</summary>
    /// <param name="isGroup">グループ (案件を含む)の行か</param>
    /// <param name="level">字下げの段 (0 の グループが案件)</param>
    /// <returns>文字の太さ</returns>
    public static Windows.UI.Text.FontWeight NameWeight(bool isGroup, int level)
        => !isGroup ? Microsoft.UI.Text.FontWeights.Normal : level == 0 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold;
}
