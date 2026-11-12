using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

/// <summary>リマインダーの送信設定の読み出し口</summary>
/// <remarks>
/// どのリマインダーをどの送信先へ送るか。
/// 書き込みはここではしない。
/// リマインダー本体と同じ 1 回の書き込みで、<see cref="IReminderRepository.AddAsync"/>・<see cref="IReminderRepository.UpdateAsync"/> が書く。
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
}
