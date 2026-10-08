using System.Text.Json;
using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>
/// Discord の Webhook へ送る。
/// </summary>
/// <param name="http">HTTP クライアント (アプリ全体で 1 つ)</param>
/// <remarks>
/// 本文は、「件名 (備考)」と、リンクがあれば、次の行にリンク。Discord の本文の上限 (2000 文字)を超える分は切る。
/// 本文の @everyone などは、メンションとして効かせない。
/// </remarks>
public sealed class DiscordWebhookNotifier(HttpClient http) : INotifier
{
    /// <summary>Discord の本文の最大の長さ</summary>
    private const int ContentMaxLength = 2000;

    /// <inheritdoc />
    public NotificationChannelKind Kind => NotificationChannelKind.Discord;

    /// <inheritdoc />
    /// <exception cref="SendFailedException">Webhook の URL が正しくない、または送れなかった。</exception>
    public Task SendAsync(NotificationChannel channel, SendMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        // 送る先を Discord の外へ向けさせないため、送る直前にも確かめる (DB の値を直接直されたときの備え)
        if (!NotificationChannelRules.IsDiscordWebhookUrl(channel.Value))
        {
            throw new SendFailedException("Discord の Webhook の URL が正しくありません。");
        }

        var content = message.Link is null ? message.Body : $"{message.Body}\n{message.Link}";
        if (content.Length > ContentMaxLength)
        {
            content = content[..ContentMaxLength];
        }

        var payload = new DiscordPayload(content, new DiscordAllowedMentions([]));
        var json = JsonSerializer.Serialize(payload, SendingJsonContext.Default.DiscordPayload);
        return NotifierHttp.PostJsonAsync(http, channel.Value, json, "Discord", cancellationToken);
    }
}
