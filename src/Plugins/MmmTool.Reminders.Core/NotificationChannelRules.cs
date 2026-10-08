namespace MmmTool.Reminders.Core;

/// <summary>送信先 (<see cref="NotificationChannel"/>)の入力の決まり</summary>
/// <remarks>長さの上限は、DB の列の長さ (<c>docs/specs/database.md</c>)と同じ。上限は保存先に関係なく、入力画面 (<c>TextBox.MaxLength</c>)でも使う。</remarks>
public static class NotificationChannelRules
{
    /// <summary>登録名の最大の長さ</summary>
    public const int NameMaxLength = 50;

    /// <summary>送る先の値の最大の長さ</summary>
    public const int ValueMaxLength = 500;

    /// <summary>ntfy のトピック名の最大の長さ (ntfy の決まり)</summary>
    public const int NtfyTopicMaxLength = 64;

    /// <summary>ntfy のトピック名の先頭</summary>
    public const string NtfyTopicPrefix = "mmmtool-";

    /// <summary>ntfy の新しいトピック名を作る</summary>
    /// <returns><c>mmmtool-</c> と、ハイフンなしの UUID (32 文字)をつなげた名前。推測できない長さのランダムな値</returns>
    public static string CreateNtfyTopic() => NtfyTopicPrefix + Guid.NewGuid().ToString("N");

    /// <summary>ntfy のトピック名として正しい形か調べる</summary>
    /// <param name="value">調べる文字列</param>
    /// <returns>半角の英字・数字・ハイフン・アンダースコアだけで、1〜64 文字なら true (ntfy の決まり)</returns>
    public static bool IsNtfyTopic(string? value)
        => !string.IsNullOrEmpty(value) && value.Length <= NtfyTopicMaxLength && value.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_');

    /// <summary>Discord の Webhook の URL として正しい形か調べる</summary>
    /// <param name="value">調べる文字列</param>
    /// <returns>Discord の Webhook の URL (<c>https://discord.com/api/webhooks/…</c>)なら true</returns>
    /// <remarks>送る先を、Discord の外へ向けさせないため、ホストも確かめる (<c>discord.com</c> と <c>discordapp.com</c>)。</remarks>
    public static bool IsDiscordWebhookUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > ValueMaxLength)
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host is "discord.com" or "discordapp.com"
            && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);
    }
}
