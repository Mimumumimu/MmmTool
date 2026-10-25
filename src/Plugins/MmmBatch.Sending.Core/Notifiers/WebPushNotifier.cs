using System.Net;
using System.Text.Json;
using MmmTool.NotificationChannels.Core;
using WebPush;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>
/// Web 版通知へ送る。ブラウザが作った宛先に、配達係 (アップル・グーグルなど)を通して届ける。
/// </summary>
/// <param name="http">HTTP クライアント (アプリ全体で 1 つ)</param>
/// <param name="keys">送り手の鍵</param>
/// <remarks>
/// 通知の中身は、宛先の鍵で暗号化して送る。配達係も中身を読めない。
/// 配達係の上限 (約 4 KB)に収まるよう、本文とリンクの長さを切り詰める。
/// 宛先が無効になった (端末が登録を解いた・アプリを消した)ときは、配達係が 404 か 410 を返す。このときは登録し直しが要る。
/// </remarks>
public sealed class WebPushNotifier(HttpClient http, WebPushKeyStore keys) : INotifier
{
    /// <summary>配達係に送る部品。アプリ全体で 1 つの <c>HttpClient</c> を使うので、これも 1 つを使い回し、後始末はしない</summary>
    private readonly WebPushClient _client = new(http);

    /// <summary>本文の最大の長さ。配達係の上限に、日本語でも収まる長さ</summary>
    private const int BodyMaxLength = 700;

    /// <summary>タイトルの最大の長さ</summary>
    private const int TitleMaxLength = 100;

    /// <summary>リンクの最大の長さ。これより長いリンクは付けない</summary>
    private const int UrlMaxLength = 500;

    /// <inheritdoc />
    public NotificationChannelKind Kind => NotificationChannelKind.WebPush;

    /// <inheritdoc />
    /// <exception cref="SendFailedException">宛先が正しくない・無効になった・鍵を読めない・送れなかった。</exception>
    public async Task SendAsync(NotificationChannel channel, SendMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        // 送る先を配達係の外へ向けさせないため、送る直前にも確かめる (DB の値を直接直されたときの備え)
        var subscription = WebPushSubscription.FromValue(channel.Value)
            ?? throw new SendFailedException("Web 版通知の宛先が正しくありません。");
        var vapid = await keys.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(CreatePayload(message), SendingJsonContext.Default.WebPushPayload);
        try
        {
            await _client.SendNotificationAsync(
                new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth), payload, vapid, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new SendFailedException("この端末の通知の宛先が無効になっています。Web 版の「通知」で、登録し直してください。", ex);
        }
        catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new SendFailedException("通知の配達係の送信の制限に達しました。しばらくしてから送り直します。", ex);
        }
        catch (WebPushException ex)
        {
            // 例外のメッセージに宛先が入ることがあるので、番号だけを伝える
            throw new SendFailedException($"通知の配達係が {(int)ex.StatusCode} を返しました。", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SendFailedException("通知の配達係につながりませんでした。", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SendFailedException("通知の配達係からの応答が時間内に返りませんでした。", ex);
        }
    }

    /// <summary>送る内容から、通知の中身を作る</summary>
    /// <param name="message">送る内容</param>
    /// <returns>切り詰めた通知の中身。リンクが URL でなければ、本文の最後に添える</returns>
    private static WebPushPayload CreatePayload(SendMessage message)
    {
        // 見出しと本文が決まっていれば、それを使う。識別子があるときは、リンクは付けない。通知を押すと、Web 版の画面で、リンクも一緒に開ける
        if (message.WebPush is { } content)
        {
            return new WebPushPayload(
                Truncate(content.Title, TitleMaxLength), Truncate(content.Body, BodyMaxLength), content.ItemKey is null ? LinkUrl(message) : null, content.ItemKey);
        }

        var body = message.Link is not null && !message.IsLinkUrl ? $"{message.Body}\n{message.Link}" : message.Body;
        return new WebPushPayload(Truncate(message.Title ?? "", TitleMaxLength), Truncate(body, BodyMaxLength), LinkUrl(message), null);
    }

    /// <summary>通知を押したときに開く URL を取り出す</summary>
    /// <param name="message">送る内容</param>
    /// <returns>開ける URL で、長さが上限以内なら、そのリンク。それ以外は null</returns>
    private static string? LinkUrl(SendMessage message)
        => message.IsLinkUrl && message.Link!.Length <= UrlMaxLength ? message.Link : null;

    /// <summary>長さを超える分を切り、切ったときは末尾に「…」を付ける</summary>
    /// <param name="text">元の文字列</param>
    /// <param name="maxLength">最大の長さ。「…」を含む</param>
    /// <returns>収めた文字列</returns>
    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 1), "…");
}
