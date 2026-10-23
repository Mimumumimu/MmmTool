using MmmTool.CliAssist.Core.Json;

namespace MmmTool.CliAssist.Core;

/// <summary>よく使う文をメモリに持ち、変更を保存する。アプリ全体で 1 つ。</summary>
/// <param name="repository">よく使う文の保存先</param>
public sealed class CliQuickMessageService(ICliQuickMessageRepository repository)
    : CliListFileService<CliQuickMessage>(repository, JsonCliQuickMessageRepository.FileName, "よく使う文", "よく使う文は表示されません。")
{
    /// <summary>よく使う文とグループ (本文が空の文は除く)</summary>
    public IReadOnlyList<CliQuickMessage> Messages => Items;

    /// <inheritdoc />
    /// <remarks>
    /// 本文が空の文を除き、表示名の空白を整える。
    /// グループの中のグループは、文として読み、本文が無ければ除く。グループは、文が 0 件でも残す。
    /// </remarks>
    protected override List<CliQuickMessage> Normalize(IEnumerable<CliQuickMessage> items)
        => [.. items.Select(NormalizeEntry).OfType<CliQuickMessage>()];

    /// <summary>文またはグループを整える</summary>
    /// <param name="entry">整える項目 (JSON の null のこともある)</param>
    /// <returns>整えた項目。使えない文は null</returns>
    private static CliQuickMessage? NormalizeEntry(CliQuickMessage? entry)
        => entry?.Items is { } children
            ? new CliQuickMessage { Label = TrimLabel(entry.Label), Items = [.. children.Select(NormalizeMessage).OfType<CliQuickMessage>()] }
            : NormalizeMessage(entry);

    /// <summary>文を整える</summary>
    /// <param name="message">整える文 (JSON の null のこともある)</param>
    /// <returns>整えた文。本文が空なら null</returns>
    private static CliQuickMessage? NormalizeMessage(CliQuickMessage? message)
        => message is { Text: { } text } && !string.IsNullOrWhiteSpace(text)
            ? new CliQuickMessage { Label = TrimLabel(message.Label), Text = text }
            : null;

    /// <summary>表示名の前後の空白を除く</summary>
    /// <param name="label">表示名</param>
    /// <returns>整えた表示名。空白だけなら null</returns>
    private static string? TrimLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();
}
