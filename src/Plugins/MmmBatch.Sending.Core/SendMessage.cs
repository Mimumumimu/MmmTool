namespace MmmBatch.Sending.Core;

/// <summary>
/// 送る内容。
/// </summary>
/// <param name="Title">タイトル。無ければ null (付けない)</param>
/// <param name="Body">本文</param>
/// <param name="Link">リンク。無ければ null</param>
/// <param name="TitleInContent">タイトルを、本文の中に入れて送るか。タイトルを持たない送信先 Discord で、タイトルを太字の 1 行目にするため</param>
/// <param name="WebPush">Web 版通知だけが使う内容。無ければ null で、タイトルと本文から作る</param>
/// <remarks>何を送るかの組み立ては、各機能が行う。リマインダーは件名・備考・リンクから、メッセージはタイトルと本文から作る。</remarks>
public sealed record SendMessage(string? Title, string Body, string? Link, bool TitleInContent = false, WebPushContent? WebPush = null)
{
    /// <summary>リンクが、通知から開ける URL (http か https)か</summary>
    /// <returns>開ける URL なら true。ファイル・フォルダーのパスなどは false</returns>
    public bool IsLinkUrl => Link is not null
        && Uri.TryCreate(Link, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>
/// Web 版通知に出す内容。通知を、何があったかが先に分かる形にする。
/// </summary>
/// <param name="Title">通知の見出し。メッセージは送った人とタイトル、リマインダーは件名</param>
/// <param name="Body">通知の本文。無ければ空文字</param>
/// <param name="ItemKey">通知の識別子。「種類:番号」の形で、Web 版の「受け取った通知」の一覧が、この通知を開くために使う。同じ識別子の通知は、端末で 1 つにまとまる。一覧に出さない通知 (テスト送信)は null</param>
/// <remarks>
/// 通知を押して開く先は、Web 版が識別子から決める。MmmBatch は、Web 版のアドレスを作らない。
/// 識別子の決まりは <c>docs/specs/web.md</c> の「通知 (Web 版)」にあり、Web 版の提供元のキー (<c>message</c>・<c>reminder</c>)と合わせる。
/// </remarks>
public sealed record WebPushContent(string Title, string Body, string? ItemKey);
