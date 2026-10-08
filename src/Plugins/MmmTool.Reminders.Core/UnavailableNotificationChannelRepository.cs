namespace MmmTool.Reminders.Core;

/// <summary>
/// 送信先 (<see cref="NotificationChannel"/>)を使えない保存先 (ローカルモード)。
/// </summary>
/// <remarks>DB モードのとき、ホストが、SQL Server の保存先に置き換える。画面は、<see cref="IsAvailable"/> が false の間は、送信先の入口を出さない。</remarks>
public sealed class UnavailableNotificationChannelRepository : INotificationChannelRepository
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public int CurrentUserId => 0;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">送信先は、DB モードだけで使える。</exception>
    public Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync(bool includeDeleted, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">送信先は、DB モードだけで使える。</exception>
    public Task<NotificationChannel?> FindAsync(int id, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">送信先は、DB モードだけで使える。</exception>
    public Task<NotificationChannel> AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">送信先は、DB モードだけで使える。</exception>
    public Task<bool> UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">送信先は、DB モードだけで使える。</exception>
    public Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <summary>使えないときの例外を作る</summary>
    /// <returns>使えないことを表す例外</returns>
    private static NotSupportedException NotAvailable() => new("送信先は、DB モードだけで使えます。");
}
