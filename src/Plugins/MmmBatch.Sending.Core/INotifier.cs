using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core;

/// <summary>外部へ送る口 (送信先の区分ごとに 1 つ)</summary>
/// <remarks>外部へ送る処理は、MmmTool には入れず、MmmBatch のこの口の実装にだけ置く。</remarks>
public interface INotifier
{
    /// <summary>この実装が送る、送信先の区分</summary>
    NotificationChannelKind Kind { get; }

    /// <summary>送る</summary>
    /// <param name="channel">送る先</param>
    /// <param name="message">送る内容</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信の完了を表すタスク (送信先が受け付けたら、正常に完了する)</returns>
    /// <exception cref="SendFailedException">送れなかった (メッセージは画面に出せる。送る先の値は含まない)。</exception>
    Task SendAsync(NotificationChannel channel, SendMessage message, CancellationToken cancellationToken = default);
}
