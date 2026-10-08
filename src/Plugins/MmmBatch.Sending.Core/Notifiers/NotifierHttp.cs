using System.Net;
using System.Text;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>送信の HTTP の、共通の処理</summary>
internal static class NotifierHttp
{
    /// <summary>JSON を POST して、成功 (2xx)でなければ、送信の失敗にする</summary>
    /// <param name="http">HTTP クライアント</param>
    /// <param name="url">送る先の URL (秘密を含みうるので、メッセージには出さない)</param>
    /// <param name="json">送る JSON</param>
    /// <param name="serviceName">失敗のメッセージに出す、送る先のサービスの名前</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信の完了を表すタスク</returns>
    /// <exception cref="SendFailedException">つながらない・時間内に返らない・成功以外の応答 (メッセージに URL は含まない)。</exception>
    public static async Task PostJsonAsync(HttpClient http, string url, string json, string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            throw response.StatusCode == HttpStatusCode.TooManyRequests
                ? new SendFailedException($"{serviceName} の送信の制限に達しました (しばらくしてから送り直します)。")
                : new SendFailedException($"{serviceName} が {(int)response.StatusCode} を返しました。");
        }
        catch (HttpRequestException ex)
        {
            // 例外のメッセージに URL が入ることがあるので、種類だけを伝える
            throw new SendFailedException($"{serviceName} につながりませんでした。", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SendFailedException($"{serviceName} からの応答が時間内に返りませんでした。", ex);
        }
    }
}
