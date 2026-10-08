using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

/// <summary>リマインダーの送信設定 (どのリマインダーを、どの送信先へ送るか)の保存先</summary>
/// <remarks>
/// DB モードだけで使う。ローカルモードでは、<see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableReminderSendSettingRepository"/>)が入り、
/// ローカルの JSON には、送信の項目を書かない (<see cref="Reminder"/> の型は変えない)。リマインダー 1 件に、送信先を複数選べる。
/// </remarks>
public interface IReminderSendSettingRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>DB モードは true、ローカルモードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool IsAvailable { get; }

    /// <summary>送信設定を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>リマインダーの番号 → 送信先の番号の一覧。送信先を選んでいない (または削除済み)リマインダーは含まない</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetChannelIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>リマインダーの送信先を設定する (選んだ送信先の一覧で置き換える)</summary>
    /// <param name="reminderNo">リマインダーの番号 (<see cref="Reminder.No"/>)</param>
    /// <param name="channelIds">選んだ送信先の番号。空なら、送信先なし (今の設定を、すべて論理削除する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>設定を終えたことを表すタスク</returns>
    /// <remarks>選んだものは、すでにある設定も含めて、更新日時を今にする (保存した時点で過ぎている今日の分を、送らないため)。選んでいないものは、論理削除する。</remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task SetChannelsAsync(int reminderNo, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken = default);
}
