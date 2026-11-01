using MmmTool.Data.SqlServer.Connection;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Users;

/// <summary>
/// 管理者がユーザーごとに決める設定 (<c>dbo.AppUserAdminSetting</c>)の保存を、SQL Server で行う。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。書くのは管理ツールだけで、作成者・更新者の列には 0 (特定のユーザーではない)を入れる。
/// 読み書きの中身は、ユーザー自身の設定と共通の <see cref="UserSettingTable"/>。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerAppUserAdminSettingRepository(SqlServerDatabase database) : IAppUserAdminSettingRepository
{
    /// <summary>作成者・更新者の列に入れる値で、特定のユーザーではないことを表す</summary>
    private const int NoUser = 0;

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public Task<string?> GetAsync(int userId, int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
        => UserSettingTable.AppUserAdminSetting.GetAsync(database, userId, featureNo, categoryNo, codeNo, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, string>> GetByFeatureAsync(int userId, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
        => UserSettingTable.AppUserAdminSetting.GetByFeatureAsync(database, userId, categoryNo, codeNo, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">番号が 1 未満・値が長すぎる。</exception>
    public Task SetAsync(int userId, int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken = default)
        => UserSettingTable.AppUserAdminSetting.SetAsync(database, userId, NoUser, featureNo, categoryNo, codeNo, value, cancellationToken);
}
