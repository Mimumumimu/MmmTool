namespace MmmTool.Users.Core;

/// <summary>
/// ローカルモードで使う、ユーザーごとの設定を使えない保存先。
/// </summary>
/// <remarks>DB モードのとき、ホストが、SQL Server の保存先に置き換える。画面は、<see cref="IsAvailable"/> が false の間は、設定の入口を出さない。</remarks>
public sealed class UnavailableAppUserSettingRepository : IAppUserSettingRepository
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">ユーザーごとの設定は、DB モードだけで使える。</exception>
    public Task<string?> GetAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">ユーザーごとの設定は、DB モードだけで使える。</exception>
    public Task SetAsync(int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">ユーザーごとの設定は、DB モードだけで使える。</exception>
    public Task ClearAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <summary>使えないときの例外を作る</summary>
    /// <returns>使えないことを表す例外</returns>
    private static NotSupportedException NotAvailable() => new("ユーザーごとの設定は、DB モードだけで使えます。");
}
