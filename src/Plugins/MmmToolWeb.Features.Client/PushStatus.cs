namespace MmmToolWeb.Features.Client;

/// <summary>
/// このブラウザが通知を受け取れるかの区分。
/// </summary>
public enum PushState
{
    /// <summary>この環境では使えない。</summary>
    Unsupported,

    /// <summary>iPhone で、ホーム画面に追加したアプリとして開いていない。</summary>
    NeedsHomeScreen,

    /// <summary>通知の許可が拒否されている。ブラウザの設定で直すまで、許可を求められない。</summary>
    Denied,

    /// <summary>登録できる。</summary>
    Ready,
}

/// <summary>
/// このブラウザの通知の状態。
/// </summary>
/// <param name="State">受け取れるかの区分。</param>
/// <param name="Subscribed">このブラウザが、今、宛先を持っているか。</param>
/// <param name="Endpoint">このブラウザが持っている宛先の URL。持っていないときは null。サーバーの登録の行を、宛先で探すために使う。</param>
public sealed record PushStatus(PushState State, bool Subscribed, string? Endpoint = null);

/// <summary>
/// ブラウザが作った通知の宛先。
/// </summary>
/// <param name="Endpoint">配達係の宛先の URL。</param>
/// <param name="P256dh">通知を暗号化する公開鍵で、base64url の文字列。</param>
/// <param name="Auth">通知を暗号化する認証の秘密で、base64url の文字列。</param>
/// <remarks>宛先を知っている人は、この端末へ通知を送れるので、秘密として扱う。</remarks>
public sealed record PushSubscriptionInfo(string Endpoint, string P256dh, string Auth);
