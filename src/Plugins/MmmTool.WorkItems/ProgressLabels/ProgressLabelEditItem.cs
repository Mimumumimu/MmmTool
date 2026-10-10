using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.WorkItems.ProgressLabels;

/// <summary>進捗度のラベルの編集欄 1 行 (値と、ラベルの入力)</summary>
/// <param name="value">進捗度の値</param>
/// <param name="label">今のラベル</param>
public sealed partial class ProgressLabelEditItem(int value, string label) : ObservableObject
{
    /// <summary>進捗度の値</summary>
    public int Value { get; } = value;

    /// <summary>値の表示 (70%)</summary>
    public string ValueText { get; } = $"{value}%";

    /// <summary>ラベル (括弧の中身。空はラベルなし)</summary>
    [ObservableProperty]
    public partial string Label { get; set; } = label;
}
