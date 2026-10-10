namespace MmmTool.WorkItems.Core;

/// <summary>進捗度の値 (0 から 100 の 10 刻みで固定)と、ラベルの付いた表記</summary>
/// <remarks>値は固定で、マスターが持つのは、値ごとのラベル (括弧の中身)を付けるか付けないか、だけ (理由は docs/specs/work-items.md)。</remarks>
public static class WorkProgress
{
    /// <summary>進捗度の値の一覧 (0, 10, ..., 100)</summary>
    public static IReadOnlyList<int> Values { get; } = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

    /// <summary>初期のラベル (値 → ラベル)。ほかの値は、ラベルなし</summary>
    public static IReadOnlyDictionary<int, string> DefaultLabels { get; } = new Dictionary<int, string>
    {
        [70] = "作成完了",
        [80] = "内部レビュー完了",
        [90] = "外部レビュー完了",
    };

    /// <summary>ラベルの最大の長さ</summary>
    public const int LabelMaxLength = 50;

    /// <summary>値が、進捗度として使える値か</summary>
    /// <param name="value">調べる値</param>
    /// <returns>0 から 100 の 10 刻みなら true</returns>
    public static bool IsValid(int value) => value is >= 0 and <= 100 && value % 10 == 0;

    /// <summary>画面に出す表記にする</summary>
    /// <param name="value">進捗度の値</param>
    /// <param name="labels">値ごとのラベル</param>
    /// <returns>ラベルがあれば <c>70% (作成完了)</c>、無ければ <c>60%</c></returns>
    public static string Format(int value, IReadOnlyDictionary<int, string> labels)
        => labels.TryGetValue(value, out var label) && label.Length > 0 ? $"{value}% ({label})" : $"{value}%";
}
