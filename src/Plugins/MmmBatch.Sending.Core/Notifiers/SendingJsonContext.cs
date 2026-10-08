using System.Text.Json.Serialization;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>ntfy の発行の内容</summary>
/// <param name="Topic">トピック名</param>
/// <param name="Title">タイトル</param>
/// <param name="Message">本文</param>
/// <param name="Click">通知をタップしたときに開く URL。無ければ省く</param>
internal sealed record NtfyPayload(string Topic, string Title, string Message, string? Click);

/// <summary>Discord の Webhook に送る内容</summary>
/// <param name="Content">本文</param>
/// <param name="AllowedMentions">メンションを許す範囲 (空で、本文の @everyone などを効かせない)</param>
internal sealed record DiscordPayload(string Content, DiscordAllowedMentions AllowedMentions);

/// <summary>Discord のメンションを許す範囲</summary>
/// <param name="Parse">メンションとして扱う種類。空なら、どれも扱わない</param>
internal sealed record DiscordAllowedMentions(string[] Parse);

/// <summary>送る内容の JSON のソース生成 (リフレクションを使わない)</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NtfyPayload))]
[JsonSerializable(typeof(DiscordPayload))]
internal sealed partial class SendingJsonContext : JsonSerializerContext;
