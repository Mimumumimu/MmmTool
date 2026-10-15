using MmmSdk.Core.Components.Storage;

namespace MmmTool.Messages.Core;

/// <summary>メッセージの宛先の読み出しと、送信依頼の保存先</summary>
/// <remarks>
/// DB モードだけで使う。ローカルモードでは、<see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableMessageSendRequestRepository"/>)が入り、画面は入口を出さない。
/// 外部へ送る処理は、MmmTool には無い。ここは、依頼の行を足すためだけにある (送るのは MmmBatch)。
/// </remarks>
public interface IMessageSendRequestRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>DB モードは true、ローカルモードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool IsAvailable { get; }

    /// <summary>宛先の一覧を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除されていない送信先すべて (自分のもの以外も含む。使用者が削除されているものは除く)。使用者の番号順、送信先の番号順</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<MessageRecipient>> GetRecipientsAsync(CancellationToken cancellationToken = default);

    /// <summary>送信依頼を足す (送信待ちで足す。送るのは MmmBatch)</summary>
    /// <param name="channelId">送る先の番号</param>
    /// <param name="title">タイトル。空文字ならタイトルなし</param>
    /// <param name="body">本文</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>足した依頼の完了を表すタスク</returns>
    /// <exception cref="ArgumentException">タイトルが長すぎる・本文が空か長すぎる。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task AddAsync(int channelId, string title, string body, CancellationToken cancellationToken = default);
}
