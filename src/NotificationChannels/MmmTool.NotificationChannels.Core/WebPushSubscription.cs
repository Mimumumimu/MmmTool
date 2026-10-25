using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// ブラウザが作った通知の宛先 (Web プッシュの購読) 1 件。
/// </summary>
/// <param name="Endpoint">配達係 (アップル・グーグルなど)の宛先の URL</param>
/// <param name="P256dh">通知を暗号化する公開鍵 (base64url)</param>
/// <param name="Auth">通知を暗号化する認証の秘密 (base64url)</param>
/// <remarks>
/// <see cref="NotificationChannelKind.WebPush"/> の送信先の値 (<see cref="NotificationChannel.Value"/>)に、JSON の文字列にして入れる。
/// 宛先を知っている人は、その端末へ通知を送れるので、秘密として扱い、画面の一覧にもログにも出さない。
/// </remarks>
public sealed record WebPushSubscription(string Endpoint, string P256dh, string Auth)
{
    /// <summary>公開鍵のバイト数。圧縮しない P-256 の点</summary>
    private const int PublicKeyLength = 65;

    /// <summary>認証の秘密の長さ (バイト)</summary>
    private const int AuthLength = 16;

    /// <summary>宛先として許す配達係のホスト (ちょうど一致)</summary>
    private static readonly string[] AllowedHosts = ["fcm.googleapis.com", "android.googleapis.com"];

    /// <summary>宛先として許す配達係のホスト (この名前の下のホスト)</summary>
    private static readonly string[] AllowedHostSuffixes = [".push.apple.com", ".push.services.mozilla.com", ".notify.windows.com"];

    /// <summary>値の文字列にする</summary>
    /// <returns>JSON の文字列。<see cref="NotificationChannel.Value"/> に入れる</returns>
    public string ToValue() => JsonSerializer.Serialize(this, WebPushJsonContext.Default.WebPushSubscription);

    /// <summary>値の文字列から読む</summary>
    /// <param name="value"><see cref="NotificationChannel.Value"/> の文字列</param>
    /// <returns>読めて、<see cref="IsValid"/> を満たせば、その宛先。そうでなければ null</returns>
    public static WebPushSubscription? FromValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > NotificationChannelRules.ValueMaxLength)
        {
            return null;
        }

        try
        {
            var subscription = JsonSerializer.Deserialize(value, WebPushJsonContext.Default.WebPushSubscription);
            return subscription is { IsValid: true } ? subscription : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>宛先として正しい形か</summary>
    /// <remarks>
    /// 送る先は登録した人が決められるので、配達係の外へ向けさせないために、ホストを確かめる。
    /// 暗号化できない値を、送信のときに初めて知ることがないよう、鍵の長さも確かめる。
    /// </remarks>
    [JsonIgnore]
    public bool IsValid =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && IsAllowedHost(uri.IdnHost)
        && DecodedLength(P256dh) == PublicKeyLength
        && DecodedLength(Auth) == AuthLength;

    /// <summary>許す配達係のホストか</summary>
    /// <param name="host">調べるホスト</param>
    /// <returns>許す配達係なら true</returns>
    private static bool IsAllowedHost(string host)
        => AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase)
        || AllowedHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>base64url の文字列を戻したときの長さ</summary>
    /// <param name="text">base64url の文字列</param>
    /// <returns>バイト数。正しい base64url でなければ -1</returns>
    private static int DecodedLength(string? text)
        => !string.IsNullOrEmpty(text) && Base64Url.IsValid(text, out var length) ? length : -1;
}

/// <summary>
/// <see cref="WebPushSubscription"/> の JSON を読み書きする、ソース生成の設定。
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(WebPushSubscription))]
internal sealed partial class WebPushJsonContext : JsonSerializerContext;
