using MmmTool.Messages.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Messages.Client;

namespace MmmToolWeb.Messages;

/// <summary>
/// メッセージの API <c>/api/messages</c>。
/// </summary>
internal static class MessageEndpoints
{
    /// <summary>
    /// メッセージの API を登録する。
    /// </summary>
    /// <param name="messages">登録先の <c>/api/messages</c>。</param>
    public static void Map(RouteGroupBuilder messages)
    {
        messages.MapGet("/form", LoadFormAsync);
        messages.MapPost("/", SendAsync);
    }

    /// <summary>
    /// 入力欄を出すのに要る情報を返す。
    /// </summary>
    /// <param name="repository">宛先の読み出しと、送信依頼の保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>宛先の一覧・署名・入力の上限。宛先に、送る先の値は含めない。</returns>
    private static async Task<IResult> LoadFormAsync(IMessageSendRequestRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        var recipients = await repository.GetRecipientsAsync(cancellationToken);
        return Results.Ok(new MessageFormResponse(
            MessageDraft.CreateSignature(currentUser.User?.DisplayName),
            MessageLimits.TitleMaxLength,
            MessageLimits.BodyMaxLength,
            [.. recipients.Select(recipient => new MessageRecipientDto(recipient.ChannelId, recipient.DisplayName))]));
    }

    /// <summary>
    /// 送信を依頼する。依頼の行を書くだけで、実際に送るのは MmmBatch。
    /// </summary>
    /// <param name="request">依頼の内容。</param>
    /// <param name="repository">宛先の読み出しと、送信依頼の保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。入力が正しくないときは 400。選んだ宛先が無いときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> SendAsync(
        MessageSendRequestDto request, IMessageSendRequestRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var body = request.Body.Trim();
        if (title.Length > MessageLimits.TitleMaxLength)
        {
            return Results.BadRequest(new ErrorResponse($"タイトルは {MessageLimits.TitleMaxLength} 文字までです。"));
        }
        if (body.Length > MessageLimits.BodyMaxLength)
        {
            return Results.BadRequest(new ErrorResponse($"本文は {MessageLimits.BodyMaxLength} 文字までです。"));
        }
        if (!MessageDraft.CanSend(hasRecipient: true, body, MessageDraft.CreateSignature(currentUser.User?.DisplayName)))
        {
            return Results.BadRequest(new ErrorResponse("本文を入力してください。"));
        }

        // 選んだあとに送信先が削除されたときは、理由を返して、宛先を選び直してもらう
        return await repository.AddAsync(request.ChannelId, title, body, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new ErrorResponse(MessageDraft.RecipientMissingMessage));
    }
}
