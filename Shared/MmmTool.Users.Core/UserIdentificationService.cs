namespace MmmTool.Users.Core;

/// <summary>
/// この PC の MAC アドレスから、今のユーザーを特定する。
/// </summary>
/// <param name="users">ユーザーの保存先</param>
/// <param name="macAddresses">この PC の MAC アドレス</param>
/// <param name="currentUser">今のユーザー (特定したら設定する)</param>
public sealed class UserIdentificationService(IAppUserRepository users, IMacAddressProvider macAddresses, CurrentUser currentUser)
{
    /// <summary>今のユーザーを特定して、<see cref="CurrentUser"/> に設定する</summary>
    /// <param name="today">今日 (使える期間の判定に使う)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>特定したユーザー。当てはまる登録が無ければ null (初回の登録が要る)</returns>
    /// <remarks>
    /// 当てはまるのは、この PC のどれかの MAC と同じで、その日に使える (<see cref="AppUser.IsActiveOn"/>)ユーザー。
    /// 複数に当てはまるときは、番号が小さいもの。
    /// </remarks>
    public async Task<AppUser?> IdentifyAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var mine = macAddresses.GetMacAddresses().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (mine.Count == 0)
        {
            return null;
        }

        var found = (await users.GetUsersAsync(cancellationToken).ConfigureAwait(false))
            .Where(user => user.IsActiveOn(today) && mine.Contains(user.MacAddress))
            .OrderBy(user => user.Id)
            .FirstOrDefault();
        if (found is not null)
        {
            currentUser.Set(found);
        }
        return found;
    }
}
