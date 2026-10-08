using System.Text.Json;
using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>
/// ntfy へ送る。
/// </summary>
/// <param name="http">HTTP クライアント (アプリ全体で 1 つ)</param>
/// <remarks>
/// サーバーの URL に JSON を POST する発行の形で送る (タイトルに日本語を使うため、ヘッダーに入れない)。
/// リンクが URL (http・https)なら、通知をタップしたときに開く。ファイル・フォルダーのパスは開けないので、本文の最後に添える。
/// </remarks>
public sealed class NtfyNotifier(HttpClient http) : INotifier
{
    /// <summary>送る先の ntfy のサーバー</summary>
    public const string ServerUrl = "https://ntfy.sh/";

    /// <inheritdoc />
    public NotificationChannelKind Kind => NotificationChannelKind.Ntfy;

    /// <inheritdoc />
    public Task SendAsync(NotificationChannel channel, SendMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        // トピック名が正しくないと、ntfy が受け付けない (DB の値を直接直されたときの備え)。メッセージに、トピック名は入れない
        if (!NotificationChannelRules.IsNtfyTopic(channel.Value))
        {
            throw new SendFailedException("ntfy のトピック名が正しくありません。");
        }

        var body = message.Link is not null && !message.IsLinkUrl ? $"{message.Body}\n{message.Link}" : message.Body;
        var payload = new NtfyPayload(channel.Value, message.Title, body, message.IsLinkUrl ? message.Link : null);
        var json = JsonSerializer.Serialize(payload, SendingJsonContext.Default.NtfyPayload);
        return NotifierHttp.PostJsonAsync(http, ServerUrl, json, "ntfy", cancellationToken);
    }
}
