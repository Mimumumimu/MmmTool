namespace MmmTool.Messages.Core;

/// <summary>
/// 宛先の読み出しと送信依頼の保存を使えない保存先 (ローカルモード)。
/// </summary>
/// <remarks>DB モードのとき、ホストが、SQL Server の保存先に置き換える。画面は、<see cref="IsAvailable"/> が false の間は、メッセージの入口を出さない。</remarks>
public sealed class UnavailableMessageSendRequestRepository : IMessageSendRequestRepository
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージの送信は、DB モードだけで使える。</exception>
    public Task<IReadOnlyList<MessageRecipient>> GetRecipientsAsync(CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージの送信は、DB モードだけで使える。</exception>
    public Task AddAsync(int channelId, string title, string body, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <summary>使えないときの例外を作る</summary>
    /// <returns>使えないことを表す例外</returns>
    private static NotSupportedException NotAvailable() => new("メッセージの送信は、DB モードだけで使えます。");
}
