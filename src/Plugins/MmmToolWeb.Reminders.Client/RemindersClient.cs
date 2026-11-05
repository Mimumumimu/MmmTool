using System.Globalization;
using System.Net.Http.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Reminders.Client;

/// <summary>
/// サーバーのリマインダーの API (<c>/api/reminders</c>)を呼ぶ。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
public sealed class RemindersClient(HttpClient http)
{
    /// <summary>リマインダーの API の道。</summary>
    private const string Path = "api/reminders";

    /// <summary>リマインダーが見つからないときの、既定の理由。</summary>
    private const string NotFoundMessage = "このリマインダーは、見つからないか、ほかの人が先に変えました。";

    /// <summary>
    /// ある日の対象と、その日の対応状態を読む。
    /// </summary>
    /// <param name="day">日。</param>
    /// <returns>その日の対象。</returns>
    public Task<ApiResult<ReminderTodayResponse>> LoadTodayAsync(DateOnly day)
        => SendAsync(
            () => http.GetAsync($"{Path}/today?day={FormatDay(day)}"),
            async response => await response.Content.ReadFromJsonAsync(RemindersJsonContext.Default.ReminderTodayResponse));

    /// <summary>
    /// 見えるリマインダーの一覧を読む。
    /// </summary>
    /// <param name="today">今日。「過去の予定」を外す基準。</param>
    /// <param name="includePast">日付が過去の日付指定も含めるか。</param>
    /// <param name="includeDeleted">削除済みも含めるか。</param>
    /// <returns>一覧。</returns>
    public Task<ApiResult<ReminderListResponse>> LoadListAsync(DateOnly today, bool includePast, bool includeDeleted)
        => SendAsync(
            () => http.GetAsync($"{Path}?day={FormatDay(today)}&includePast={Bool(includePast)}&includeDeleted={Bool(includeDeleted)}"),
            async response => await response.Content.ReadFromJsonAsync(RemindersJsonContext.Default.ReminderListResponse));

    /// <summary>
    /// 入力のシートが要る、宛先と送信先の選択肢と、文字数の上限を読む。
    /// </summary>
    /// <param name="includeTarget">編集するリマインダーの宛先の人の番号。もう選べない人のときも、選択肢に含めるため。無いときは 0。</param>
    /// <returns>選択肢と上限。</returns>
    public Task<ApiResult<ReminderOptionsResponse>> LoadOptionsAsync(int includeTarget)
        => SendAsync(
            () => http.GetAsync($"{Path}/options?target={includeTarget}"),
            async response => await response.Content.ReadFromJsonAsync(RemindersJsonContext.Default.ReminderOptionsResponse));

    /// <summary>
    /// この端末が登録した、自分の送信先の番号を探す。
    /// </summary>
    /// <param name="endpoint">この端末のブラウザが持つ、通知の宛先。</param>
    /// <returns>送信先の番号。登録していないときは 0。</returns>
    public Task<ApiResult<ReminderDeviceResponse>> FindThisDeviceAsync(string endpoint)
        => SendAsync(
            () => http.PostAsJsonAsync($"{Path}/this-device", new ReminderDeviceRequest(endpoint), RemindersJsonContext.Default.ReminderDeviceRequest),
            async response => await response.Content.ReadFromJsonAsync(RemindersJsonContext.Default.ReminderDeviceResponse));

    /// <summary>
    /// 編集のために、リマインダー 1 件の中身を読む。
    /// </summary>
    /// <param name="no">リマインダーの番号。</param>
    /// <returns>中身。見えない・無いときは、見つからない結果。</returns>
    public Task<ApiResult<ReminderDetailDto>> LoadDetailAsync(int no)
        => SendAsync(
            () => http.GetAsync($"{Path}/{no}"),
            async response => await response.Content.ReadFromJsonAsync(RemindersJsonContext.Default.ReminderDetailDto));

    /// <summary>
    /// リマインダーを保存する。
    /// </summary>
    /// <param name="no">編集するリマインダーの番号。新規のときは null。</param>
    /// <param name="request">保存する内容。</param>
    /// <returns>保存の結果。保存したリマインダーの番号と、送信先を保存できなかったときの理由を含む。</returns>
    public Task<ApiResult<ReminderSaveResult>> SaveAsync(int? no, ReminderSaveRequest request)
        => SendAsync(
            () => no is { } existing
                ? http.PutAsJsonAsync($"{Path}/{existing}", request, RemindersJsonContext.Default.ReminderSaveRequest)
                : http.PostAsJsonAsync(Path, request, RemindersJsonContext.Default.ReminderSaveRequest),
            async response => await response.Content.ReadFromJsonAsync(RemindersJsonContext.Default.ReminderSaveResult));

    /// <summary>
    /// リマインダーを削除する。
    /// </summary>
    /// <param name="no">リマインダーの番号。</param>
    /// <returns>操作の結果。</returns>
    public Task<ApiResult<bool>> DeleteAsync(int no)
        => SendAsync(
            () => http.PostAsJsonAsync($"{Path}/{no}/deleted", new ReminderDeletedRequest(true), RemindersJsonContext.Default.ReminderDeletedRequest),
            _ => Task.FromResult(true));

    /// <summary>日付を、要求に使う文字にする。</summary>
    /// <param name="day">日。</param>
    /// <returns><c>yyyy-MM-dd</c> の文字。</returns>
    private static string FormatDay(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>真偽を、要求に使う文字にする。</summary>
    /// <param name="value">値。</param>
    /// <returns><c>true</c> か <c>false</c>。</returns>
    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>
    /// 要求を送って、結果を、画面に出せる形にする。
    /// </summary>
    /// <typeparam name="T">成功したときの中身。</typeparam>
    /// <param name="send">要求を送る処理。</param>
    /// <param name="read">成功の応答から、中身を読む処理。</param>
    /// <returns>結果。</returns>
    private static Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, Func<HttpResponseMessage, Task<T?>> read)
        => ApiCaller.SendAsync(send, read, RemindersJsonContext.Default.ErrorResponse, NotFoundMessage);
}
