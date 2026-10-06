using MmmSdk.Core.Components.Secrets;
using MmmSdk.Db.SqlServer.Components.Connections;
using MmmTool.Data.Connection;

namespace MmmTool.Data.SqlServer.Connection;

/// <summary>
/// 接続の設定とパスワードから、SQL Server への接続を作るファクトリを作る。
/// </summary>
/// <remarks>
/// 設定は <see cref="DatabaseSettingsService"/> から、そのつど読む。足りない設定は、画面に出せるメッセージの
/// <see cref="DatabaseSettingsException"/> にする (利用者の設定漏れで、バグではない)。ログインの方式が Windows のときは、ユーザー名とパスワードを使わない。
/// </remarks>
/// <param name="settings">DB への接続の設定</param>
public sealed class SqlServerConnectionFactoryBuilder(DatabaseSettingsService settings)
{
    /// <summary>今の設定から、接続を作るファクトリを作る</summary>
    /// <returns>接続を作るファクトリ</returns>
    /// <exception cref="DatabaseSettingsException">サーバー名・データベース名・ユーザー名が空、またはパスワードが未登録。</exception>
    /// <exception cref="SecretStoreException">秘密の保管庫を読めなかった。</exception>
    public SqlServerConnectionFactory Build()
    {
        var value = settings.Load();

        if (string.IsNullOrWhiteSpace(value.Server))
        {
            throw new DatabaseSettingsException("サーバー名が設定されていません。");
        }
        if (string.IsNullOrWhiteSpace(value.Name))
        {
            throw new DatabaseSettingsException("データベース名が設定されていません。");
        }

        string? userName = null;
        string? password = null;
        if (value.Authentication == DatabaseAuthentication.Sql)
        {
            if (string.IsNullOrWhiteSpace(value.UserName))
            {
                throw new DatabaseSettingsException("ユーザー名が設定されていません。");
            }
            password = settings.GetPassword();
            if (string.IsNullOrEmpty(password))
            {
                throw new DatabaseSettingsException("パスワードが登録されていません。");
            }
            userName = value.UserName;
        }

        return new SqlServerConnectionFactory(new SqlServerConnectionOptions
        {
            Server = value.Server,
            Database = value.Name,
            Authentication = value.Authentication == DatabaseAuthentication.Windows ? SqlServerAuthentication.Windows : SqlServerAuthentication.Sql,
            UserName = userName,
            Password = password,
            TrustServerCertificate = value.TrustServerCertificate,
        });
    }
}
