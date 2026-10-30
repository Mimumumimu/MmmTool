using MmmSdk.Core.Components.Storage;

namespace MmmTool.Messages.Core;

/// <summary>
/// 会話とメッセージの保存先。読み出しと、メッセージの書き込みを行う。
/// </summary>
/// <remarks>
/// DB モードだけで使う。ローカルモードでは、<see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableMessageRepository"/>)が入り、画面は入口を出さない。
/// 外部へ送る処理は、MmmTool には無い。ここは、メッセージと通知の状態の行を書くためだけにある。送るのは MmmBatch。
/// 操作する人は、今のユーザー。特定していないと、読み書きできない。
/// </remarks>
public interface IMessageRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>DB モードは true、ローカルモードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool IsAvailable { get; }

    /// <summary>自分の会話の一覧を、最初に読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>自分の会話すべての要約と、次の差分の基準の日時。メッセージは空</returns>
    /// <remarks>メッセージの無い会話は含めない。要約は、最後のメッセージが新しい順。</remarks>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<MessageSyncResult> GetStartAsync(CancellationToken cancellationToken = default);

    /// <summary>会話のメッセージを、新しい順に取得する</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="beforeMessageId">この番号より小さいメッセージだけを読む。0 のときは最新から</param>
    /// <param name="count">読む最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>メッセージ。新しい順。自分が参加していない会話は、空</returns>
    /// <remarks>ページの番号ではなく、読んだ中で一番小さい番号で区切る。読んでいる間に新しいメッセージが届いても、同じ行が重ならず、抜けない。</remarks>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(int conversationId, int beforeMessageId, int count, CancellationToken cancellationToken = default);

    /// <summary>基準より後に更新された、会話とメッセージを取得する</summary>
    /// <param name="since">この日時より後に更新された行を読む。呼ぶ側が、前回の基準から <see cref="MessageSync.Lookback"/> を戻した日時を渡す</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>自分宛のメッセージ・自分が送ったメッセージ・自分が新しく入った会話の、変わった分と、次の基準の日時</returns>
    /// <remarks>会話の数に関係なく、索引 3 回で読む。同じ行が何度届いても、受け取る側は、番号と更新日時で突き合わせて、結果を変えない。</remarks>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<MessageSyncResult> GetChangesAsync(DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>相手との 1 対 1 の会話を探す</summary>
    /// <param name="peerUserId">相手の番号。自分の番号なら、自分との会話</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>会話の番号。まだ無ければ 0。作らない</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<int> FindDirectConversationAsync(int peerUserId, CancellationToken cancellationToken = default);

    /// <summary>メッセージが属する会話を調べる。通知を押したときに、開く会話を決める</summary>
    /// <param name="messageId">メッセージの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>自分が参加している会話の番号。メッセージが無い・自分が参加していないときは 0</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<int> FindConversationOfMessageAsync(int messageId, CancellationToken cancellationToken = default);

    /// <summary>宛先に選べる人を、表示名の前方一致で読む</summary>
    /// <param name="prefix">表示名の先頭の文字。空文字のときは、全員</param>
    /// <param name="count">読む最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>受け取り先を指定している人。自分も含む。表示名の順</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<MessagePerson>> SearchPeopleAsync(string prefix, int count, CancellationToken cancellationToken = default);

    /// <summary>その人が、受け取り先を指定しているか調べる</summary>
    /// <param name="userId">人の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>指定していて、その送信先が削除されていないとき true</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<bool> CanReceiveAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>自分が入っている会話の、1 対 1 の相手が、受け取り先を指定しているか調べる</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>相手が受け取れるとき true。グループは、相手が 1 人に決まらないので true。自分が入っていない会話・無い会話は null。自分との会話は、自分の状態</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<bool?> CanPeerReceiveAsync(int conversationId, CancellationToken cancellationToken = default);

    /// <summary>メッセージを送る</summary>
    /// <param name="conversationId">会話の番号。まだ会話が無いときは 0</param>
    /// <param name="peerUserId">会話の番号が 0 のとき、1 対 1 の相手の番号。自分の番号なら、自分に送る</param>
    /// <param name="body">本文。前後の空白は取って書く</param>
    /// <param name="replyToMessageId">返信元のメッセージの番号。返信でなければ 0</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>結果。送れたときは、書いた会話とメッセージの番号</returns>
    /// <remarks>
    /// 1 つのトランザクションで、メッセージと、送った人以外の参加者ごとの通知の状態 (自分との会話は自分に 1 行)を書く。
    /// 会話の番号が 0 のときは、1 対 1 の会話を、2 人の組ごとの鍵のもとで探し、無ければ作る。同時に始めても、1 つだけになる。
    /// 1 対 1 は、相手が受け取り先を指定していないとき、何も書かない。
    /// </remarks>
    /// <exception cref="ArgumentException">本文が空か長すぎる・会話の番号と相手の番号の指定が正しくない。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    Task<SendMessageResult> SendAsync(int conversationId, int peerUserId, string body, int replyToMessageId, CancellationToken cancellationToken = default);
}
