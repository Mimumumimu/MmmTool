using System.Net.Http.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>
/// サーバーのメッセージの API (<c>/api/messages</c>)を呼ぶ。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
public sealed class MessagesClient(HttpClient http)
{
    /// <summary>メッセージの API の道。</summary>
    private const string Path = "api/messages";

    /// <summary>選んだ宛先が無いときの、既定の理由。</summary>
    private const string NotFoundMessage = "選んだ送信先がありません。宛先を選び直してください。";

    /// <summary>
    /// 入力欄を出すのに要る情報 (宛先の一覧・署名・入力の上限)を読む。
    /// </summary>
    /// <returns>入力欄の情報。</returns>
    public Task<ApiResult<MessageFormResponse>> LoadFormAsync()
        => CallAsync(
            () => http.GetAsync($"{Path}/form"),
            async response => await response.Content.ReadFromJsonAsync(MessagesJsonContext.Default.MessageFormResponse));

    /// <summary>
    /// 送信を依頼する。依頼の行が書かれるだけで、実際に送るのは MmmBatch。
    /// </summary>
    /// <param name="request">依頼の内容。</param>
    /// <returns>依頼の結果。</returns>
    public Task<ApiResult<bool>> SendAsync(MessageSendRequestDto request)
        => CallAsync(
            () => http.PostAsJsonAsync(Path, request, MessagesJsonContext.Default.MessageSendRequestDto),
            _ => Task.FromResult(true));

    /// <summary>
    /// 要求を送って、結果を、画面に出せる形にする。
    /// </summary>
    /// <typeparam name="T">成功したときの中身。</typeparam>
    /// <param name="send">要求を送る処理。</param>
    /// <param name="read">成功の応答から、中身を読む処理。</param>
    /// <returns>結果。</returns>
    private static Task<ApiResult<T>> CallAsync<T>(Func<Task<HttpResponseMessage>> send, Func<HttpResponseMessage, Task<T?>> read)
        => ApiCaller.SendAsync(send, read, MessagesJsonContext.Default.ErrorResponse, NotFoundMessage);
}
