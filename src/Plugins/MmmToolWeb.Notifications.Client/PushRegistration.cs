using System.Globalization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notifications.Client;

/// <summary>
/// この端末が、通知を受け取る端末として登録済みかを調べる。通知の画面と、設定の画面の「通知」の節が使う。
/// </summary>
/// <param name="notifications">サーバーの通知の API。</param>
/// <param name="browser">ブラウザの部品。</param>
/// <remarks>
/// 登録済みと見なすのは、ブラウザが宛先を持っていて、サーバーにその宛先の行があるとき。ブラウザの宛先だけでは、登録済みにしない。
/// 登録に失敗した端末や、行が削除された端末が、受け取れると表示されないため。
/// 登録の番号の記憶 (<c>localStorage</c>)は、手がかりの 1 つ。iPhone が保存領域を消して記憶を失っても、宛先で探し直して、番号を覚え直す。
/// </remarks>
public sealed class PushRegistration(NotificationsClient notifications, BrowserClient browser)
{
    /// <summary>この端末が登録した送信先の番号を覚える、この端末の値の名前。</summary>
    public const string ChannelIdKey = "notifications.channelId";

    /// <summary>
    /// この端末の登録済みの送信先の番号を返す。
    /// </summary>
    /// <param name="status">この端末の通知の状態。</param>
    /// <param name="devices">サーバーの、自分が登録した端末の一覧。</param>
    /// <returns>登録済みの送信先の番号。登録済みでないときは 0。</returns>
    public async Task<int> FindRegisteredIdAsync(PushStatus status, IReadOnlyList<PushDeviceDto> devices)
    {
        if (!status.Subscribed || string.IsNullOrEmpty(status.Endpoint))
        {
            return 0;
        }

        // 覚えている番号の行が、サーバーの一覧にあれば、問い合わせずに済む
        if (int.TryParse(await browser.GetRememberedAsync(ChannelIdKey), out var remembered)
            && remembered > 0
            && devices.Any(device => device.Id == remembered))
        {
            return remembered;
        }

        // 記憶が無い・古いときは、宛先で探す。見つけたら、番号を覚え直す
        var result = await notifications.LookupAsync(new PushLookupRequest(status.Endpoint));
        if (!result.Succeeded || result.Value!.Id <= 0)
        {
            return 0;
        }

        await browser.RememberAsync(ChannelIdKey, result.Value.Id.ToString(CultureInfo.InvariantCulture));
        return result.Value.Id;
    }
}
