using MmmSdk.Core.Utilities;

namespace MmmTool.Core.CliAssist;

/// <summary>
/// 送信履歴。先頭が最新。
/// </summary>
/// <remarks>プライバシーのため保存せず、起動中だけメモリに持つ。スレッドセーフではないので、UI スレッドから使う。</remarks>
public sealed class SendHistory
{
    /// <summary>履歴の最大件数</summary>
    public const int MaxCount = 50;

    /// <summary>履歴（先頭が最新）</summary>
    private readonly List<SendHistoryEntry> _entries = [];

    /// <summary>送信した本文を履歴の先頭に追加する</summary>
    /// <param name="text">送信した本文</param>
    /// <param name="sentAt">送信日時</param>
    /// <remarks>空白のみは無視し、同じ本文は先頭へ移す。最大件数を超えた古いものは捨てる。</remarks>
    public void Add(string text, DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _entries.AddRecent(new SendHistoryEntry(text, sentAt), entry => entry.Text == text, MaxCount);
    }

    /// <summary>絞り込み文字列に合う履歴を返す</summary>
    /// <param name="filter">絞り込み文字列（前後の空白は無視。大文字小文字は区別しない）。空なら全件</param>
    /// <returns>合う履歴（新しい順）</returns>
    public IReadOnlyList<SendHistoryEntry> Search(string filter)
    {
        var trimmed = filter.Trim();
        return trimmed.Length == 0
            ? [.. _entries]
            : [.. _entries.Where(entry => entry.Text.Contains(trimmed, StringComparison.OrdinalIgnoreCase))];
    }
}
