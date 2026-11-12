using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

/// <summary>リマインダーの送信設定の保存先</summary>
/// <remarks>
/// どのリマインダーをどの送信先へ送るか。
/// DB モードだけで使う。
/// ローカルモードでは <see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableReminderSendSettingRepository"/>)が入り、
/// ローカルの JSON には送信の項目を書かない。
/// <see cref="Reminder"/> の型は変えない。
/// リマインダー 1 件に送信先を複数選べる。
/// </remarks>
public interface IReminderSendSettingRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>
    /// DB モードは true、ローカルモードは false。
    /// false のとき、ほかの操作は <see cref="NotSupportedException"/>。
    /// </remarks>
    bool IsAvailable { get; }

    /// <summary>送信設定を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// リマインダーの番号 → 送信先の番号の一覧
    /// 自分が使用者の送信先の分だけ
    /// 送信先を選んでいない (または削除済み)リマインダーは含まない
    /// </returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetChannelIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>リマインダーの送信先を設定する</summary>
    /// <param name="reminderNo">リマインダーの番号 (<see cref="Reminder.No"/>)</param>
    /// <param name="channelIds">
    /// 選んだ送信先の番号。
    /// 空なら、送信先なし (今の設定を、すべて論理削除する)。
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>設定を終えたことを表すタスク</returns>
    /// <remarks>
    /// 選んだ送信先の一覧で置き換える。
    /// 選んだものはすでにある設定も含めて、更新日時を今にする。
    /// 保存した時点で過ぎている今日の分を送らないため。
    /// 選んでいないものは論理削除する。
    /// 対象は自分が使用者の送信先だけで、ほかの人の送信先の設定は足さず、外さない。
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task SetChannelsAsync(int reminderNo, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken = default);

    /// <summary>複数のリマインダーに同じ送信先を設定する</summary>
    /// <param name="reminderNos">リマインダーの番号</param>
    /// <param name="channelIds">
    /// 選んだ送信先の番号。
    /// 空なら、送信先なし。
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>設定を終えたことを表すタスク</returns>
    /// <remarks>
    /// 選んだ送信先の一覧で置き換える。
    /// リマインダーの数だけ <see cref="SetChannelsAsync(int, IReadOnlyCollection{int}, CancellationToken)"/> を呼ぶ代わりに使う。
    /// 全部を 1 つのトランザクションの 1 回の往復で書く。
    /// 選んだものの更新日時と、対象にする送信先は 1 件のときと同じ。
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task SetChannelsAsync(IReadOnlyCollection<int> reminderNos, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken = default);
}
