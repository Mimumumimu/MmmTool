namespace MmmTool.Messages.Core;

/// <summary>
/// ローカルモードで使う、会話とメッセージを使えない保存先。
/// </summary>
/// <remarks>DB モードのとき、ホストが、SQL Server の保存先に置き換える。画面は、<see cref="IsAvailable"/> が false の間は、メッセージの入口を出さない。</remarks>
public sealed class UnavailableMessageRepository : IMessageRepository
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<MessageSyncResult> GetStartAsync(CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(int conversationId, int beforeMessageId, int count, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<MessageSyncResult> GetChangesAsync(DateTimeOffset since, CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<int> FindDirectConversationAsync(int peerUserId, CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<int> FindConversationOfMessageAsync(int messageId, CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<IReadOnlyList<MessagePerson>> SearchPeopleAsync(string prefix, int count, CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<bool> CanReceiveAsync(int userId, CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<bool?> CanPeerReceiveAsync(int conversationId, CancellationToken cancellationToken = default) => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">メッセージは、DB モードだけで使える。</exception>
    public Task<SendMessageResult> SendAsync(int conversationId, int peerUserId, string body, int replyToMessageId, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <summary>使えないときの例外を作る</summary>
    /// <returns>使えないことを表す例外</returns>
    private static NotSupportedException NotAvailable() => new("メッセージは、DB モードだけで使えます。");
}
