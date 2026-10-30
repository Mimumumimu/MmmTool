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

    /// <summary>見つからないときの、既定の理由。</summary>
    private const string NotFoundMessage = "会話が見つかりません。";

    /// <summary>
    /// 入力欄を出すのに要る情報 (本文の上限と、自分と相手が受け取れるか)を読む。
    /// </summary>
    /// <param name="conversationId">開いている会話の番号。まだ会話が無いときは 0。</param>
    /// <param name="peerUserId">まだ会話が無いときの、1 対 1 の相手の番号。</param>
    /// <returns>入力欄の情報。</returns>
    public Task<ApiResult<MessageFormDto>> LoadFormAsync(int conversationId, int peerUserId)
        => CallAsync(() => http.GetAsync($"{Path}/form?conversationId={conversationId}&peerUserId={peerUserId}"), response => Read(response, MessagesJsonContext.Default.MessageFormDto));

    /// <summary>
    /// 自分の会話の一覧を、最初に読み込む。
    /// </summary>
    /// <returns>会話の要約と、次の差分の基準の日時。</returns>
    public Task<ApiResult<MessageSyncDto>> LoadStartAsync()
        => CallAsync(() => http.GetAsync($"{Path}/conversations"), response => Read(response, MessagesJsonContext.Default.MessageSyncDto));

    /// <summary>
    /// 基準より後に更新された、会話とメッセージを読む。
    /// </summary>
    /// <param name="nextBase">前回の結果の次の基準の日時。サーバーが戻して読む。</param>
    /// <returns>変わった分と、次の基準の日時。</returns>
    public Task<ApiResult<MessageSyncDto>> LoadChangesAsync(DateTimeOffset nextBase)
        => CallAsync(
            () => http.GetAsync($"{Path}/changes?since={Uri.EscapeDataString(nextBase.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}"),
            response => Read(response, MessagesJsonContext.Default.MessageSyncDto));

    /// <summary>
    /// 会話のメッセージを 1 ページ読む。
    /// </summary>
    /// <param name="conversationId">会話の番号。</param>
    /// <param name="before">この番号より小さいメッセージだけを読む。0 なら最新から。</param>
    /// <returns>古いものが先のメッセージ。</returns>
    public Task<ApiResult<MessagePageDto>> LoadMessagesAsync(int conversationId, int before)
        => CallAsync(
            () => http.GetAsync($"{Path}/conversations/{conversationId}/messages" + (before > 0 ? $"?before={before}" : "")),
            response => Read(response, MessagesJsonContext.Default.MessagePageDto));

    /// <summary>
    /// 相手との 1 対 1 の会話を探す。
    /// </summary>
    /// <param name="userId">相手の番号。</param>
    /// <returns>会話の番号。無ければ 0。</returns>
    public Task<ApiResult<DirectDto>> FindDirectAsync(int userId)
        => CallAsync(() => http.GetAsync($"{Path}/direct?userId={userId}"), response => Read(response, MessagesJsonContext.Default.DirectDto));

    /// <summary>
    /// 宛先に選べる人を、表示名の前方一致で読む。
    /// </summary>
    /// <param name="prefix">表示名の先頭の文字。空文字のときは、全員。</param>
    /// <returns>受け取り先を指定している人。自分も含む。</returns>
    public Task<ApiResult<PersonDto[]>> SearchPeopleAsync(string prefix)
        => CallAsync(
            () => http.GetAsync($"{Path}/people/search?q={Uri.EscapeDataString(prefix)}"),
            response => Read(response, MessagesJsonContext.Default.PersonDtoArray));

    /// <summary>
    /// メッセージを送る。メッセージと通知の状態の行が書かれるだけで、実際に送るのは MmmBatch。
    /// </summary>
    /// <param name="request">送る内容。</param>
    /// <returns>書いた会話とメッセージの番号。</returns>
    public Task<ApiResult<MessageSentDto>> SendAsync(MessageSendDto request)
        => CallAsync(
            () => http.PostAsJsonAsync(Path, request, MessagesJsonContext.Default.MessageSendDto),
            response => Read(response, MessagesJsonContext.Default.MessageSentDto));

    /// <summary>
    /// 成功の応答から、中身を読む。
    /// </summary>
    /// <typeparam name="T">中身の型。</typeparam>
    /// <param name="response">成功の応答。</param>
    /// <param name="typeInfo">中身の型の、ソース生成の情報。</param>
    /// <returns>中身。</returns>
    private static Task<T?> Read<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        => response.Content.ReadFromJsonAsync(typeInfo);

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
