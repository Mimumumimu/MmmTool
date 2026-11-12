using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>送信とメッセージの表を読み書きするときの共通の決まり</summary>
internal static class SendTableAccess
{
    /// <summary>重複したキーのエラー番号</summary>
    public static readonly int[] DuplicateKeyErrors = [2601, 2627];

    /// <summary>まだ送っていない行の SentAt の値</summary>
    /// <remarks>
    /// 9999-12-31 で、docs/specs/database.md の「未送信は 9999-12-31」と同じ。
    /// 送った行だけを新しい順に読むとき、この値より前の範囲を、索引で探す。
    /// 未送信・失敗の行は降順では先頭に並ぶので範囲から外す。
    /// </remarks>
    public static readonly DateTimeOffset NotSentAt = new(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);

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

    /// <summary>今のユーザーの番号を取得する</summary>
    /// <param name="currentUser">今のユーザー</param>
    /// <returns>今のユーザーの番号</returns>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない
    /// ログインが必要
    /// </exception>
    public static int RequireUserId(CurrentUser currentUser)
    {
        const string Message = "ユーザーが特定されていないため、送信の機能を使えません。ログインが必要です。";
        return currentUser.User?.Id ?? throw new DataFileException(Message, new InvalidOperationException(Message));
    }
}
