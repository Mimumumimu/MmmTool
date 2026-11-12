namespace MmmTool.Reminders.Core;

/// <summary>リマインダーを保存するときに書く送信先を決める</summary>
/// <remarks>
/// PC 版 (<see cref="ReminderService"/>)と Web 版が同じ決まりで書くよう、1 か所に置く。
/// </remarks>
public static class ReminderSendChannels
{
    /// <summary>保存する送信先の番号を決める</summary>
    /// <param name="targetUserId">
    /// リマインダーの宛先の人の番号
    /// 全員宛ては 0
    /// </param>
    /// <param name="selfId">今のユーザーの番号</param>
    /// <param name="selected">選んだ送信先の番号</param>
    /// <returns>
    /// 宛先が自分か全員宛てなら、選んだ送信先。
    /// それ以外は空。
    /// </returns>
    /// <remarks>
    /// 宛先をほかの人にしたリマインダーは自分の送信先へ送らない。
    /// 空にすると、自分が使用者の送信先の設定がすべて外れる。
    /// </remarks>
    public static IReadOnlyCollection<int> For(int targetUserId, int selfId, IReadOnlyCollection<int> selected)
        => targetUserId == 0 || targetUserId == selfId ? selected : [];
}
