namespace MmmToolWeb.Auth.Core.Passkeys;

/// <summary>
/// パスキーの保存先。
/// </summary>
public interface IAppUserPasskeyRepository
{
    /// <summary>
    /// 識別番号からパスキーを探す。
    /// </summary>
    /// <param name="credentialId">パスキーの識別番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>見つかったパスキー。無いときは <c>null</c>。</returns>
    Task<AppUserPasskey?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    /// <summary>
    /// 1 人の削除されていないパスキーをすべて読む。
    /// </summary>
    /// <param name="appUserId">ユーザーの番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>パスキーの一覧。</returns>
    Task<IReadOnlyList<AppUserPasskey>> GetByUserAsync(int appUserId, CancellationToken cancellationToken);

    /// <summary>
    /// パスキーを新しく保存する。
    /// </summary>
    /// <param name="passkey">保存するパスキー (<see cref="AppUserPasskey.Id"/> は無視する)。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>保存の完了を表すタスク。</returns>
    Task AddAsync(AppUserPasskey passkey, CancellationToken cancellationToken);

    /// <summary>
    /// ログインに使ったときの使用回数と日時を更新する。
    /// </summary>
    /// <param name="id">パスキーの番号。</param>
    /// <param name="signCount">新しい使用回数。</param>
    /// <param name="lastUsedAt">使った日時 (UTC)。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>更新の完了を表すタスク。</returns>
    Task UpdateUsageAsync(int id, uint signCount, DateTime lastUsedAt, CancellationToken cancellationToken);
}
