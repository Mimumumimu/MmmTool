using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// サーバーの API を呼んだ結果。
/// </summary>
/// <typeparam name="T">成功したときの中身。</typeparam>
/// <param name="Value">成功したときの中身。</param>
/// <param name="Message">失敗したときに画面に出す文。成功のときは null。</param>
/// <param name="Status">失敗の種類。</param>
public sealed record ApiResult<T>(T? Value, string? Message, ApiFailure Status)
{
    /// <summary>成功したか。</summary>
    public bool Succeeded => Status == ApiFailure.None;
}

/// <summary>
/// API の失敗の種類。
/// </summary>
public enum ApiFailure
{
    /// <summary>失敗していない。</summary>
    None,

    /// <summary>ログインしていない・ログインが切れた。</summary>
    SignedOut,

    /// <summary>対象が見つからない。ほかの人が先に変えた・自分には見えない。</summary>
    NotFound,

    /// <summary>入力が正しくない。</summary>
    Invalid,

    /// <summary>通信できない・サーバーが使えない。</summary>
    Unavailable,
}

/// <summary>
/// 失敗の理由。画面に出せる文。
/// </summary>
/// <param name="Message">理由。</param>
public sealed record ErrorResponse(string Message);

/// <summary>
/// 機能の API を呼んで、結果を、画面に出せる形にする。
/// </summary>
public static class ApiCaller
{
    /// <summary>
    /// 要求を送って、結果を、画面に出せる形にする。
    /// </summary>
    /// <typeparam name="T">成功したときの中身。</typeparam>
    /// <param name="send">要求を送る処理。</param>
    /// <param name="read">成功の応答から、中身を読む処理。</param>
    /// <param name="errorInfo">失敗の理由 (<see cref="ErrorResponse"/>)を読む、機能のソース生成の設定。</param>
    /// <param name="notFoundMessage">見つからないとき、サーバーが理由を返さなかった場合の文。</param>
    /// <returns>結果。</returns>
    public static async Task<ApiResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, Task<T?>> read,
        JsonTypeInfo<ErrorResponse> errorInfo,
        string notFoundMessage)
    {
        try
        {
            using var response = await send();
            if (response.IsSuccessStatusCode)
            {
                return new ApiResult<T>(await read(response), null, ApiFailure.None);
            }
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new ApiResult<T>(default, "ログインが切れました。", ApiFailure.SignedOut),
                HttpStatusCode.NotFound => new ApiResult<T>(default, await ReadMessageAsync(response, errorInfo) ?? notFoundMessage, ApiFailure.NotFound),
                HttpStatusCode.BadRequest => new ApiResult<T>(default, await ReadMessageAsync(response, errorInfo) ?? "入力が正しくありません。", ApiFailure.Invalid),
                _ => new ApiResult<T>(default, "サーバーを使えません。しばらくしてから、もう一度お試しください。", ApiFailure.Unavailable),
            };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>(default, "通信できませんでした。", ApiFailure.Unavailable);
        }
    }

    /// <summary>
    /// 失敗の応答から、画面に出せる理由を読む。
    /// </summary>
    /// <param name="response">失敗の応答。</param>
    /// <param name="errorInfo">失敗の理由を読む設定。</param>
    /// <returns>理由。読めないときは null。</returns>
    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response, JsonTypeInfo<ErrorResponse> errorInfo)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync(errorInfo))?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
