using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// 送信とメッセージの表 (<c>dbo.NotificationChannel</c>・<c>dbo.ReminderSendSetting</c>・<c>dbo.AppUserSetting</c>・<c>dbo.Message</c> など)を読み書きするときの、共通の決まり。
/// </summary>
/// <remarks>
/// 表は、管理者が <c>001_CreateTables.sql</c> と、リリース後の変更のスクリプトを流して作る。まだ流していないとき、SQL のエラーのまま出さず、流す手順を伝えるメッセージの
/// <see cref="DataFileException"/> にする (画面は、これを InfoBar に出して、送信先の入口を使えないままにする)。
/// </remarks>
internal static class SendTableAccess
{
    /// <summary>オブジェクト名が正しくない (表が無い)ときの SQL Server のエラー番号</summary>
    private const int InvalidObjectName = 208;

    /// <summary>重複したキー (一意制約・一意インデックス)のエラー番号</summary>
    public static readonly int[] DuplicateKeyErrors = [2601, 2627];

    /// <summary>まだ送っていない行の <c>SentAt</c> の値。<c>9999-12-31</c> で、docs/specs/database.md の「未送信は 9999-12-31」と同じ</summary>
    /// <remarks>送った行だけを新しい順に読むとき、この値より前の範囲を、索引で探す。未送信・失敗の行は、降順では先頭に並ぶので、範囲から外す。</remarks>
    public static readonly DateTimeOffset NotSentAt = new(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>接続を開いて、送信の表を使う処理を実行する</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・実行中に失敗した (メッセージは画面に出せる)。</exception>
    public static Task<T> RunAsync<T>(SqlServerDatabase database, Func<SqlConnection, Task<T>> work, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            try
            {
                return await work(connection).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                throw new DataFileException("送信の表がデータベースにありません。管理者に、001_CreateTables.sql と、006_AddWebPushKind.sql を実行してもらってください。", ex);
            }
        }, cancellationToken);

    /// <summary>今のユーザーの番号を取得する</summary>
    /// <param name="currentUser">今のユーザー</param>
    /// <returns>今のユーザーの番号</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない (ログインが必要)。</exception>
    public static int RequireUserId(CurrentUser currentUser)
    {
        const string Message = "ユーザーが特定されていないため、送信の機能を使えません (ログインが必要です)。";
        return currentUser.User?.Id ?? throw new DataFileException(Message, new InvalidOperationException(Message));
    }
}
