using System.Globalization;
using System.Net.Http.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notes.Client;

/// <summary>
/// サーバーのノートの API (<c>/api/notes</c>)を呼ぶ。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
public sealed class NotesClient(HttpClient http)
{
    /// <summary>ノートの API の道。</summary>
    private const string Path = "api/notes";

    /// <summary>ノートが見つからないときの、既定の理由。</summary>
    private const string NotFoundMessage = "ノートが見つかりません。";

    /// <summary>
    /// 本文の色のスタイルシートの道。
    /// </summary>
    public static string StylesPath => Path + "/styles.css";

    /// <summary>
    /// ある日に有効なノートを読む。
    /// </summary>
    /// <param name="day">日。</param>
    /// <param name="includeDeleted">削除したものも含めるか。</param>
    /// <returns>ノートの一覧。</returns>
    public Task<ApiResult<NoteListResponse>> LoadAsync(DateOnly day, bool includeDeleted)
        => SendAsync(
            () => http.GetAsync($"{Path}?day={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&includeDeleted={(includeDeleted ? "true" : "false")}"),
            async response => await response.Content.ReadFromJsonAsync(NotesJsonContext.Default.NoteListResponse));

    /// <summary>
    /// ノートを保存する。
    /// </summary>
    /// <param name="id">編集するノートの番号。新規のときは null。</param>
    /// <param name="request">保存する内容。</param>
    /// <returns>保存の結果。</returns>
    public Task<ApiResult<bool>> SaveAsync(int? id, NoteSaveRequest request)
        => SendAsync(
            () => id is { } existing
                ? http.PutAsJsonAsync($"{Path}/{existing}", request, NotesJsonContext.Default.NoteSaveRequest)
                : http.PostAsJsonAsync(Path, request, NotesJsonContext.Default.NoteSaveRequest),
            _ => Task.FromResult(true));

    /// <summary>
    /// ノートを削除する・復元する。
    /// </summary>
    /// <param name="id">ノートの番号。</param>
    /// <param name="isDeleted">削除するなら true、復元するなら false。</param>
    /// <returns>操作の結果。</returns>
    public Task<ApiResult<bool>> SetDeletedAsync(int id, bool isDeleted)
        => SendAsync(
            () => http.PostAsJsonAsync($"{Path}/{id}/deleted", new NoteDeletedRequest(isDeleted), NotesJsonContext.Default.NoteDeletedRequest),
            _ => Task.FromResult(true));

    /// <summary>
    /// 要求を送って、結果を、画面に出せる形にする。
    /// </summary>
    /// <typeparam name="T">成功したときの中身。</typeparam>
    /// <param name="send">要求を送る処理。</param>
    /// <param name="read">成功の応答から、中身を読む処理。</param>
    /// <returns>結果。</returns>
    private static Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, Func<HttpResponseMessage, Task<T?>> read)
        => ApiCaller.SendAsync(send, read, NotesJsonContext.Default.ErrorResponse, NotFoundMessage);
}
