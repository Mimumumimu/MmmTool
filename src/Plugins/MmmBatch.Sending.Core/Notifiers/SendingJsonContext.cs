using System.Text.Json.Serialization;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>ntfy の発行の内容</summary>
/// <param name="Topic">トピック名</param>
/// <param name="Title">タイトル。無ければ省く</param>
/// <param name="Message">本文</param>
/// <param name="Click">通知をタップしたときに開く URL。無ければ省く</param>
internal sealed record NtfyPayload(string Topic, string? Title, string Message, string? Click);

/// <summary>Discord の Webhook に送る内容</summary>
/// <param name="Content">本文</param>
/// <param name="AllowedMentions">メンションを許す範囲 (空で、本文の @everyone などを効かせない)</param>
internal sealed record DiscordPayload(string Content, DiscordAllowedMentions AllowedMentions);

/// <summary>Discord のメンションを許す範囲</summary>
/// <param name="Parse">メンションとして扱う種類。空なら、どれも扱わない</param>
internal sealed record DiscordAllowedMentions(string[] Parse);

/// <summary>Web 版通知の内容。ブラウザのサービスワーカー (<c>sw.js</c>)が読む</summary>
/// <param name="Title">タイトル。無ければ空文字で、サービスワーカーが既定の名前を出す</param>
/// <param name="Body">本文</param>
/// <param name="Url">通知を押したときに開く URL。無ければ省く。識別子があるときは付けない</param>
/// <param name="Key">通知の識別子。あれば、通知を押すと、Web 版の「通知」の画面でこの通知を開く。同じ識別子の通知は、端末で 1 つにまとまる。無ければ省く</param>
internal sealed record WebPushPayload(string Title, string Body, string? Url, string? Key);

/// <summary>送る内容の JSON のソース生成 (リフレクションを使わない)</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NtfyPayload))]
[JsonSerializable(typeof(DiscordPayload))]
[JsonSerializable(typeof(WebPushPayload))]
internal sealed partial class SendingJsonContext : JsonSerializerContext;
