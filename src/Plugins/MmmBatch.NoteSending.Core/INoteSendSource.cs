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

    /// <summary>ノートが、いまも通知の対象か確かめる</summary>
    /// <param name="noteId">ノートの番号</param>
    /// <param name="today">今日。終了日がこの日以降かを見る</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除されておらず、公開範囲が「全員」で、終了日が今日以降なら true。送る予定に入れたあとで変わったノートを、送る直前に外すために使う</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<bool> IsNoticeTargetAsync(int noteId, DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>通知する相手を、人の番号の順に、指定の人数まで読む</summary>
    /// <param name="afterUserId">この番号より後の人を読む。最初は 0</param>
    /// <param name="excludeUserId">除く人。ノートを最後に更新した人</param>
    /// <param name="count">読む最大の人数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 「ノートの通知を受け取る」がオンで、通知先が指定されていて、その送信先が削除されておらず、管理者がノートを無効にしていない人。
    /// 全員を一度に読まないよう、人数ずつ読む。
    /// </returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<NoteRecipient>> GetRecipientsAsync(int afterUserId, int excludeUserId, int count, CancellationToken cancellationToken = default);

    /// <summary>指定した人のうち、いまも通知する相手である人を、1 回で読む</summary>
    /// <param name="userIds">調べる人の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// <see cref="GetRecipientsAsync"/> と同じ条件を満たす人と、いまの通知先。条件を外れた人は含まない。人の番号の順。
    /// ノートを最後に更新した人を除く判断はしない。送り直しの予定に入る前に、除いてあるため。
    /// </returns>
    /// <remarks>人の数だけ <see cref="GetRecipientsAsync"/> を呼ぶ代わりに使う。送り直しの前に、相手が条件を満たしているかを確かめるために使う。</remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<NoteRecipient>> GetRecipientsByIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);
}
