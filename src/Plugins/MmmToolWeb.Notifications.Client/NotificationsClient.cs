using System.Net.Http.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notifications.Client;

/// <summary>
/// サーバーの通知の API (<c>/api/notifications</c>)を呼ぶ。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
public sealed class NotificationsClient(HttpClient http)
{
    /// <summary>通知の API の道。</summary>
    private const string Path = "api/notifications";

    /// <summary>対象が見つからないときの、既定の理由。</summary>
    private const string NotFoundMessage = "登録が見つかりません。画面を開き直してください。";

    /// <summary>
    /// 画面を出すのに要る情報 (送り手の公開鍵・登録済みの端末)を読む。
    /// </summary>
    /// <returns>画面の情報。</returns>
    public Task<ApiResult<PushSettingsResponse>> LoadAsync()
        => CallAsync(
            () => http.GetAsync($"{Path}/settings"),
            async response => await response.Content.ReadFromJsonAsync(NotificationsJsonContext.Default.PushSettingsResponse));

    /// <summary>
    /// 受け取った通知の一覧を読む。
    /// </summary>
    /// <param name="item">通知を押して開いたときの、その通知の識別子。一覧に無いほど古いときも、一覧に加えて返す。無ければ null。</param>
    /// <returns>新しい順の通知。</returns>
    public Task<ApiResult<ReceivedListResponse>> LoadReceivedAsync(string? item)
        => CallAsync(
            () => http.GetAsync(item is null ? $"{Path}/received" : $"{Path}/received?item={Uri.EscapeDataString(item)}"),
            async response => await response.Content.ReadFromJsonAsync(NotificationsJsonContext.Default.ReceivedListResponse));

    /// <summary>
    /// 通知を押したときに開く画面を、種類ごとの行き先の登録に聞く。
    /// </summary>
    /// <param name="item">通知の識別子。「種類:番号」の形。</param>
    /// <returns>開く画面の道。行き先が無ければ、道は null。</returns>
    public Task<ApiResult<DestinationResponse>> GetDestinationAsync(string item)
        => CallAsync(
            () => http.GetAsync($"{Path}/destination?item={Uri.EscapeDataString(item)}"),
            async response => await response.Content.ReadFromJsonAsync(NotificationsJsonContext.Default.DestinationResponse));

    /// <summary>
    /// この端末の宛先を登録する。
    /// </summary>
    /// <param name="request">登録する内容。</param>
    /// <returns>登録した送信先の番号。</returns>
    public Task<ApiResult<PushRegisterResponse>> RegisterAsync(PushRegisterRequest request)
        => CallAsync(
            () => http.PostAsJsonAsync($"{Path}/subscriptions", request, NotificationsJsonContext.Default.PushRegisterRequest),
            async response => await response.Content.ReadFromJsonAsync(NotificationsJsonContext.Default.PushRegisterResponse));

    /// <summary>
    /// この端末の宛先が、自分の登録済みの行かを、サーバーに聞く。
    /// </summary>
    /// <param name="request">調べる宛先。</param>
    /// <returns>登録済みの送信先の番号。無いときは 0。</returns>
    public Task<ApiResult<PushLookupResponse>> LookupAsync(PushLookupRequest request)
        => CallAsync(
            () => http.PostAsJsonAsync($"{Path}/subscriptions/lookup", request, NotificationsJsonContext.Default.PushLookupRequest),
            async response => await response.Content.ReadFromJsonAsync(NotificationsJsonContext.Default.PushLookupResponse));

    /// <summary>
    /// この端末の登録を解除する。
    /// </summary>
    /// <param name="request">解除する送信先。</param>
    /// <returns>解除の結果。</returns>
    public Task<ApiResult<bool>> RemoveAsync(PushRemoveRequest request)
        => CallAsync(
            () => http.PostAsJsonAsync($"{Path}/subscriptions/remove", request, NotificationsJsonContext.Default.PushRemoveRequest),
            _ => Task.FromResult(true));

    /// <summary>
    /// 要求を送って、結果を、画面に出せる形にする。
    /// </summary>
    /// <typeparam name="T">成功したときの中身。</typeparam>
    /// <param name="send">要求を送る処理。</param>
    /// <param name="read">成功の応答から、中身を読む処理。</param>
    /// <returns>結果。</returns>
    private static Task<ApiResult<T>> CallAsync<T>(Func<Task<HttpResponseMessage>> send, Func<HttpResponseMessage, Task<T?>> read)
        => ApiCaller.SendAsync(send, read, NotificationsJsonContext.Default.ErrorResponse, NotFoundMessage);
}
