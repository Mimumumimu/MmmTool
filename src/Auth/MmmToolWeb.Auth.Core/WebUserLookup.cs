using MmmTool.Users.Core;

namespace MmmToolWeb.Auth.Core;

/// <summary>
/// Web 版にログインできる人 (今日使える状態のユーザー)を探す。
/// </summary>
/// <param name="users">ユーザーの保存先。</param>
public sealed class WebUserLookup(IAppUserRepository users)
{
    /// <summary>
    /// 番号から、今日使える状態のユーザーを探す。
    /// </summary>
    /// <param name="appUserId">ユーザーの番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>使える状態のユーザー。見つからない・削除済み・使える期間の外のときは <c>null</c>。</returns>
    public async Task<AppUser?> FindActiveAsync(int appUserId, CancellationToken cancellationToken)
    {
        var found = await users.GetUsersByIdsAsync([appUserId], cancellationToken);
        var user = found.FirstOrDefault();
        return user is not null && user.IsActiveOn(DateOnly.FromDateTime(DateTime.Today)) ? user : null;
    }

    /// <summary>
    /// ログイン名から、今日使える状態のユーザーを探す。
    /// </summary>
    /// <param name="loginName">ログイン名。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>使える状態のユーザー。見つからない・使える期間の外のときは <c>null</c>。</returns>
    public async Task<AppUser?> FindActiveByLoginNameAsync(string loginName, CancellationToken cancellationToken)
    {
        var user = await users.FindByLoginNameWithoutPasswordAsync(loginName, cancellationToken);
        return user is not null && user.IsActiveOn(DateOnly.FromDateTime(DateTime.Today)) ? user : null;
    }
}
