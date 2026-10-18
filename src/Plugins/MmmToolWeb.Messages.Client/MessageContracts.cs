namespace MmmToolWeb.Messages.Client;

/// <summary>
/// 宛先 1 件。
/// </summary>
/// <param name="ChannelId">送信先の番号。</param>
/// <param name="DisplayName">画面に出す名前。「ユーザー名 - 登録名 (ntfy)」の形。</param>
/// <remarks>送る先の値であるトピック名や Webhook の URL は、秘密なので持たない。</remarks>
public sealed record MessageRecipientDto(int ChannelId, string DisplayName);

/// <summary>
/// 入力欄を出すのに要る情報。
/// </summary>
/// <param name="Signature">本文の最初の入力。改行 2 つと「○○より」で、今のユーザーが分からなければ空文字。</param>
/// <param name="TitleMaxLength">タイトルの最大の長さ。</param>
/// <param name="BodyMaxLength">本文の最大の長さ。</param>
/// <param name="Recipients">選べる宛先。</param>
public sealed record MessageFormResponse(string Signature, int TitleMaxLength, int BodyMaxLength, IReadOnlyList<MessageRecipientDto> Recipients);

/// <summary>
/// 送信を依頼する要求。
/// </summary>
/// <param name="ChannelId">送る先の番号。</param>
/// <param name="Title">タイトル。空文字ならタイトルなし。</param>
/// <param name="Body">本文。</param>
public sealed record MessageSendRequestDto(int ChannelId, string Title, string Body);
