using Microsoft.UI.Xaml.Controls;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProgressLabels;

/// <summary>進捗度のラベルを編集するダイアログ</summary>
public sealed partial class ProgressLabelsDialog : ContentDialog
{
    /// <summary>値ごとの入力欄 (0 から 100 の 11 件)</summary>
    public IReadOnlyList<ProgressLabelEditItem> Items { get; private set; } = [];

    /// <summary>ダイアログを作る</summary>
    public ProgressLabelsDialog() => InitializeComponent();

    /// <summary>ダイアログを開き、編集後のラベルを返す</summary>
    /// <param name="labels">今のラベル (値 → ラベル)</param>
    /// <returns>編集後のラベル (値 → ラベル)。キャンセルなら null</returns>
    public async Task<IReadOnlyDictionary<int, string>?> EditAsync(IReadOnlyDictionary<int, string> labels)
    {
        Items = [.. WorkProgress.Values.Select(v => new ProgressLabelEditItem(v, labels.GetValueOrDefault(v, "")))];
        Bindings.Update();
        var result = await ShowAsync();
        return result == ContentDialogResult.Primary
            ? Items.ToDictionary(i => i.Value, i => i.Label)
            : null;
    }
}
