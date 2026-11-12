namespace MmmTool.Reminders.Core;

/// <summary>リマインダーの送信設定を使えない保存先で、ローカルモードのもの</summary>
/// <remarks>
/// DB モードではホストが SQL Server の保存先に置き換える
/// <see cref="IsAvailable"/> が false の間は画面に送信先の欄を出さない
/// </remarks>
public sealed class UnavailableReminderSendSettingRepository : IReminderSendSettingRepository
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">送信設定は DB モードだけで使える</exception>
    public Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetChannelIdsAsync(CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <summary>使えないときの例外を作る</summary>
    /// <returns>使えないことを表す例外</returns>
    private static NotSupportedException NotAvailable() => new("送信設定は、DB モードだけで使えます。");
}
