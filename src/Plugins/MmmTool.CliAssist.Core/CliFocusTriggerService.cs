using MmmTool.CliAssist.Core.Json;

namespace MmmTool.CliAssist.Core;

/// <summary>通知する文字列をメモリに持ち、変更を保存する。アプリ全体で 1 つ。</summary>
/// <param name="repository">通知する文字列の保存先</param>
public sealed class CliFocusTriggerService(ICliFocusTriggerRepository repository)
    : CliListFileService<CliFocusTrigger>(repository, JsonCliFocusTriggerRepository.FileName, "通知する文字列", "通知する文字列は使われません。")
{
    /// <summary>通知する文字列と、ターミナルへ移るかの設定 (空の項目は除く)</summary>
    public IReadOnlyList<CliFocusTrigger> Triggers => [.. Items];

    /// <inheritdoc />
    /// <remarks>文字列が空の項目を除き、前後の空白を除く。</remarks>
    protected override List<CliFocusTrigger> Normalize(IEnumerable<CliFocusTrigger> items)
        => [.. items
            .Where(trigger => trigger is { Text: { } text } && !string.IsNullOrWhiteSpace(text))
            .Select(trigger => new CliFocusTrigger { Text = trigger.Text!.Trim(), MoveToTerminal = trigger.MoveToTerminal })];
}
