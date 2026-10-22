using MmmSdk.Core.Components.Storage;
using MmmTool.NotificationChannels.Core;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders;

/// <summary>
/// リマインダーの画面で選ぶ送信先の、チェックのリストの選択肢を作る。入力画面とまとめて追加の画面が共有する。
/// </summary>
/// <param name="channels">送信先の保存先</param>
/// <param name="sendSettings">リマインダーの送信設定の保存先</param>
/// <param name="reminders">リマインダーの読み書き</param>
public sealed class ReminderChannelLoader(
    INotificationChannelRepository channels, IReminderSendSettingRepository sendSettings, ReminderService reminders)
{
    /// <summary>送信先を選ぶ欄を出せるか</summary>
    /// <remarks>DB モードで、今のユーザーを特定できているときだけ true。</remarks>
    public bool IsAvailable => channels.IsAvailable && sendSettings.IsAvailable && reminders.CurrentUserId != 0;

    /// <summary>自分が登録した送信先を、選択肢にして返す</summary>
    /// <param name="checkedIds">最初にチェックを入れる送信先の番号</param>
    /// <returns>自分が登録した送信先の選択肢。ほかの人の送信先は含まない</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public async Task<IReadOnlyList<ReminderChannelOption>> LoadMineAsync(IReadOnlySet<int> checkedIds)
    {
        var mine = await channels.GetChannelsAsync(includeDeleted: false);
        return [.. mine.Select(channel => new ReminderChannelOption(channel.Id, channel.Kind, channel.Name, checkedIds.Contains(channel.Id)))];
    }
}
