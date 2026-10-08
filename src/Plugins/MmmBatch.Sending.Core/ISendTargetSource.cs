using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>送る対象 (送信先を選んだリマインダー)の読み出し口</summary>
public interface ISendTargetSource
{
    /// <summary>送信先を選んでいる、削除されていないリマインダーと、その送信先を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送る対象。日付・時刻で絞る前の、すべて (絞るのは <see cref="ReminderSendService"/>)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<SendTarget>> GetTargetsAsync(CancellationToken cancellationToken = default);
}
