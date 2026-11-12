using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Logging;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Db.SqlServer.Components.Connections;
using MmmTool.Data.Connection;

namespace MmmTool.Data.SqlServer.Connection;

/// <summary>SQL Server に接続して処理を実行する入口</summary>
/// <remarks>
/// アプリ全体で 1 つ
/// 接続は処理のたびに開いて閉じる
/// 実体は接続プールが使い回す
/// 接続を作るファクトリは最初に成功したものを使い回す
/// 設定の変更は次の起動から反映する
/// 再起動せずに反映したいときは <see cref="ResetConnection"/> を呼ぶ
/// 接続の失敗・設定の不足・実行中の失敗は画面に出せるメッセージの
/// <see cref="DataFileException"/> にして渡す
/// 画面はほかの保存先と同じ <see cref="DataFileException"/> を受ける
/// </remarks>
/// <param name="builder">接続を作るファクトリを作る処理</param>
/// <param name="errorLog">
/// SQL Server が返した失敗を、例外ごと書くログ。
/// 登録していないアプリ (ログを別の仕組みで書くもの)では null で、書かない。
/// </param>
public sealed class SqlServerDatabase(SqlServerConnectionFactoryBuilder builder, ErrorLog? errorLog = null)
{
    private readonly Lock _gate = new();
    private SqlServerConnectionFactory? _factory;

    /// <summary>接続を開いて処理を実行する</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・実行中に失敗した
    /// メッセージは画面に出せる
    /// </exception>
    public async Task<T> RunAsync<T>(Func<SqlConnection, Task<T>> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        var databaseName = "";
        try
        {
            await using var connection = await GetFactory().OpenAsync(cancellationToken).ConfigureAwait(false);
            databaseName = connection.Database;
            return await work(connection).ConfigureAwait(false);
        }
        catch (DatabaseSettingsException ex)
        {
            throw new DataFileException(ex.Message, ex);
        }
        catch (SecretStoreException ex)
        {
            throw new DataFileException(ex.Message, ex);
        }
        catch (SqlServerConnectionException ex)
        {
            throw new DataFileException(ex.Message, ex);
        }
        catch (SqlException ex)
        {
            // どのサーバー・どの DB で、どのエラー番号かを残す。呼び出しの流れ (どの機能の処理か)は、例外のスタックトレースに入る
            errorLog?.Write($"データベースの読み書きに失敗 (サーバー: {ex.Server} / データベース: {databaseName} / エラー番号: {ex.Number})", ex);
            throw new DataFileException($"データベースの読み書きに失敗しました。{ex.Message}", ex);
        }
    }

    /// <summary>接続を開いて結果を返さない処理を実行する</summary>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・実行中に失敗した
    /// メッセージは画面に出せる
    /// </exception>
    public Task RunAsync(Func<SqlConnection, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return RunAsync(async connection =>
        {
            await work(connection).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <summary>覚えている接続のファクトリを捨てて、次の操作で、今の設定から作り直させる</summary>
    /// <remarks>
    /// 接続の設定を保存したあとに呼ぶ
    /// 再起動せずに新しい設定を使うため
    /// 実行中の操作はそのまま続く
    /// </remarks>
    public void ResetConnection()
    {
        lock (_gate)
        {
            _factory = null;
        }
    }

    /// <summary>接続を作るファクトリを取得する</summary>
    /// <returns>接続を作るファクトリ</returns>
    /// <remarks>まだ作っていなければ、今の設定から作る。</remarks>
    /// <exception cref="DatabaseSettingsException">サーバー名・データベース名・ユーザー名が空、またはパスワードが未登録。</exception>
    /// <exception cref="SecretStoreException">秘密の保管庫を読めなかった</exception>
    private SqlServerConnectionFactory GetFactory()
    {
        lock (_gate)
        {
            return _factory ??= builder.Build();
        }
    }
}
