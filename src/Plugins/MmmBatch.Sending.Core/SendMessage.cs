namespace MmmBatch.Sending.Core;

/// <summary>
/// 送る内容。
/// </summary>
/// <param name="Title">タイトル。無ければ null (付けない)</param>
/// <param name="Body">本文</param>
/// <param name="Link">リンク。無ければ null</param>
/// <param name="TitleInContent">タイトルを、本文の中に入れて送るか (タイトルを持たない送信先 (Discord)で、タイトルを太字の 1 行目にするため)</param>
/// <remarks>何を送るかの組み立ては、各機能が行う (リマインダーは件名・備考・リンクから、メッセージはタイトルと本文から)。</remarks>
public sealed record SendMessage(string? Title, string Body, string? Link, bool TitleInContent = false)
{
    /// <summary>リンクが、通知から開ける URL (http か https)か</summary>
    /// <returns>開ける URL なら true。ファイル・フォルダーのパスなどは false</returns>
    public bool IsLinkUrl => Link is not null
        && Uri.TryCreate(Link, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
