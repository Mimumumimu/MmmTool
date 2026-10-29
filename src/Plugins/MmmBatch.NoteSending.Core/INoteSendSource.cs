using MmmTool.Notes.Core;

namespace MmmBatch.NoteSending.Core;

/// <summary>
/// ノートの通知の元になる、更新されたノートと、通知する相手の読み出し口。読むだけで、DB には書かない。
/// </summary>
/// <remarks>DB の実装は <c>MmmBatch.Data.SqlServer</c> にあり、ホストが登録する。</remarks>
public interface INoteSendSource
{
    /// <summary>基準の時刻の後に更新されたノートを読む</summary>
    /// <param name="since">この時刻より後に更新されたものを読む。null のときは、ノートを読まず、基準にするデータベースサーバーの時刻だけを返す</param>
    /// <param name="today">今日。終了日がこの日以降のノートだけを読む</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新されたノート (更新日時の古い順)と、読んだ時点のデータベースサーバーの時刻</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>ノートを更新した人の表示名を読む</summary>
    /// <param name="userId">更新した人 (<c>AppUser.Id</c>)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>表示名。引けなかったときは空文字</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<string> GetUserNameAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>通知する相手を、人の番号の順に、指定の人数まで読む</summary>
    /// <param name="afterUserId">この番号より後の人を読む。最初は 0</param>
    /// <param name="excludeUserId">除く人。ノートを最後に更新した人</param>
    /// <param name="count">読む最大の人数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 「ノートの通知を受け取る」がオンで、受け取り先が指定されていて、その送信先が削除されておらず、管理者がノートを無効にしていない人。
    /// 全員を一度に読まないよう、人数ずつ読む。
    /// </returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<NoteRecipient>> GetRecipientsAsync(int afterUserId, int excludeUserId, int count, CancellationToken cancellationToken = default);
}
