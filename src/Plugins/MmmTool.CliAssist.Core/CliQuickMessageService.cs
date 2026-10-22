using MmmTool.CliAssist.Core.Json;

namespace MmmTool.CliAssist.Core;

/// <summary>よく使う文をメモリに持ち、変更を保存する。アプリ全体で 1 つ。</summary>
/// <param name="repository">よく使う文の保存先</param>
public sealed class CliQuickMessageService(ICliQuickMessageRepository repository)
    : CliListFileService<CliQuickMessage>(repository, JsonCliQuickMessageRepository.FileName, "よく使う文", "よく使う文は表示されません。")
{
    /// <summary>よく使う文 (本文が空の項目は除く)</summary>
    public IReadOnlyList<CliQuickMessage> Messages => Items;

    /// <inheritdoc />
    /// <remarks>本文が空の項目を除き、表示名の空白を整える。</remarks>
    protected override List<CliQuickMessage> Normalize(IEnumerable<CliQuickMessage> items)
        => [.. items
            .Where(message => message is { Text: { } text } && !string.IsNullOrWhiteSpace(text))
            .Select(message => new CliQuickMessage
            {
                Label = string.IsNullOrWhiteSpace(message.Label) ? null : message.Label.Trim(),
                Text = message.Text,
            })];
}
