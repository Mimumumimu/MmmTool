using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Main;

/// <summary>右のペインの、変更の記録の 1 行</summary>
/// <param name="change">変更の記録</param>
public sealed class WorkChangeItem(WorkItemChange change)
{
    /// <summary>変えた日時</summary>
    public string TimeText { get; } = change.ChangedAt.ToString("yyyy/MM/dd HH:mm");

    /// <summary>項目の名前</summary>
    public string Field { get; } = change.Field;

    /// <summary>変更の内容 (変更前 → 変更後)</summary>
    public string Text { get; } = $"{Display(change.OldValue)} → {Display(change.NewValue)}";

    /// <summary>値を、記録の表示にする</summary>
    /// <param name="value">変更前・変更後の値</param>
    /// <returns>空なら「なし」、複数行なら先頭の 1 行 (省略記号つき)、そうでなければそのまま</returns>
    private static string Display(string value)
    {
        if (value.Length == 0)
        {
            return "(なし)";
        }
        var firstLine = value.Split('\n', 2)[0].TrimEnd('\r');
        return firstLine.Length == value.Length ? value : firstLine + " …";
    }
}
