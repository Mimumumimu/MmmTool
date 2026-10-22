using MmmTool.CliAssist.Core.Json;

namespace MmmTool.CliAssist.Core;

/// <summary>ターミナルへ移る文字列をメモリに持ち、変更を保存する。アプリ全体で 1 つ。</summary>
/// <param name="repository">ターミナルへ移る文字列の保存先</param>
public sealed class CliFocusTriggerService(ICliFocusTriggerRepository repository)
    : CliListFileService<CliFocusTrigger>(repository, JsonCliFocusTriggerRepository.FileName, "ターミナルへ移る文字列", "ターミナルへ移る文字列は使われません。")
{
    /// <summary>ターミナルへ移る文字列 (空の項目は除く)</summary>
    public IReadOnlyList<string> Texts => [.. Items.Select(trigger => trigger.Text!)];

    /// <inheritdoc />
    /// <remarks>文字列が空の項目を除き、前後の空白を除く。</remarks>
    protected override List<CliFocusTrigger> Normalize(IEnumerable<CliFocusTrigger> items)
        => [.. items
            .Where(trigger => trigger is { Text: { } text } && !string.IsNullOrWhiteSpace(text))
            .Select(trigger => new CliFocusTrigger { Text = trigger.Text!.Trim() })];
}
