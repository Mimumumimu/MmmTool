using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>送信の表を読み書きするときの共通の決まり</summary>
/// <remarks><c>dbo.ReminderSendStatus</c>と MmmTool が作る送信先・送信設定・メッセージの送信依頼の表</remarks>
internal static class SendTableAccess
{
    /// <summary>重複したキーのエラー番号</summary>
    public static readonly int[] DuplicateKeyErrors = [2601, 2627];

    /// <summary>接続を開いて送信の表を使う処理を実行する</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・実行中に失敗した
    /// メッセージは画面に出せる
    /// 画面はこれを InfoBar に出す
    /// </exception>
    public static Task<T> RunAsync<T>(SqlServerDatabase database, Func<SqlConnection, Task<T>> work, CancellationToken cancellationToken)
        => database.RunAsync(work, cancellationToken);
}
