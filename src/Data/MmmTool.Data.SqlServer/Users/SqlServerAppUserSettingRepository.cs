using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Users;

/// <summary>
/// ユーザーごとの設定 (<c>dbo.AppUserSetting</c>)の保存を、SQL Server で行う。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。操作するのは今のユーザー自身の設定だけで、特定していないと使えない。
/// 読み書きの中身は、管理者が決める設定と共通の <see cref="UserSettingTable"/>。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerAppUserSettingRepository(SqlServerDatabase database, CurrentUser currentUser) : IAppUserSettingRepository
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public Task<string?> GetAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
        => UserSettingTable.AppUserSetting.GetAsync(database, SendTableAccess.RequireUserId(currentUser), featureNo, categoryNo, codeNo, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, string>> GetByFeatureAsync(int categoryNo, int codeNo, CancellationToken cancellationToken = default)
        => UserSettingTable.AppUserSetting.GetByFeatureAsync(database, SendTableAccess.RequireUserId(currentUser), categoryNo, codeNo, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">番号が 1 未満・値が長すぎる。</exception>
    public Task SetAsync(int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return UserSettingTable.AppUserSetting.SetAsync(database, userId, userId, featureNo, categoryNo, codeNo, value, cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return UserSettingTable.AppUserSetting.ClearAsync(database, userId, userId, featureNo, categoryNo, codeNo, cancellationToken);
    }
}
