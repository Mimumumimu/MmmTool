using System.Globalization;
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
    /// <summary>宛先に選べる人を、1 回に返す最大の件数</summary>
    private const int SearchCount = 100;

    /// <summary>
    /// メッセージの API を登録する。
    /// </summary>
    /// <param name="messages">登録先の <c>/api/messages</c>。</param>
    public static void Map(RouteGroupBuilder messages)
    {
        messages.MapGet("/form", LoadFormAsync);
        messages.MapGet("/conversations", StartAsync);
        messages.MapGet("/conversations/{conversationId:int}/messages", LoadMessagesAsync);
        messages.MapGet("/changes", LoadChangesAsync);
        messages.MapGet("/direct", FindDirectAsync);
        messages.MapGet("/people/search", SearchPeopleAsync);
        messages.MapPost("/", SendAsync);
        messages.MapGet("/events", Events);
    }

    /// <summary>
    /// 自分の会話の一覧を、最初に読み込む。
    /// </summary>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>会話の要約と、次の差分の基準の日時。</returns>
    private static async Task<IResult> StartAsync(IMessageRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
        => Results.Ok(ToDto(await repository.GetStartAsync(cancellationToken), currentUser.Id));

    /// <summary>
    /// 会話のメッセージを 1 ページ読む。
    /// </summary>
    /// <param name="conversationId">会話の番号。</param>
    /// <param name="before">この番号より小さいメッセージだけを読む。省略は最新から。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>古いものが先のメッセージ。参加者でない会話は 404。</returns>
    private static async Task<IResult> LoadMessagesAsync(
        int conversationId, int? before, IMessageRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        // 1 件多く読んで、これより古いメッセージがあるかを調べる
        var newestFirst = await repository.GetMessagesAsync(conversationId, before ?? 0, MessageSync.PageSize + 1, cancellationToken);
        if (newestFirst.Count == 0 && (before ?? 0) == 0)
        {
            return Results.NotFound(new ErrorResponse("会話が見つかりません。"));
        }

        var hasMore = newestFirst.Count > MessageSync.PageSize;
        var now = DateTimeOffset.Now;
        var page = newestFirst.Take(MessageSync.PageSize).Reverse().Select(message => ToDto(message, currentUser.Id, now)).ToList();
        return Results.Ok(new MessagePageDto(page, hasMore));
    }

    /// <summary>
    /// 基準より後に更新された、会話とメッセージを返す。
    /// </summary>
    /// <param name="since">前回の結果の次の基準の日時。サーバーが戻して読む。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>変わった分と、次の基準の日時。</returns>
    private static async Task<IResult> LoadChangesAsync(
        DateTimeOffset since, IMessageRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
        => Results.Ok(ToDto(await repository.GetChangesAsync(MessageSync.SinceOf(since), cancellationToken), currentUser.Id));

    /// <summary>
    /// 相手との 1 対 1 の会話を探す。無ければ作らず、0 を返す。
    /// </summary>
    /// <param name="userId">相手の番号。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>会話の番号。無ければ 0。</returns>
    private static async Task<IResult> FindDirectAsync(int userId, IMessageRepository repository, CancellationToken cancellationToken)
        => Results.Ok(new DirectDto(await repository.FindDirectConversationAsync(userId, cancellationToken)));

    /// <summary>
    /// 入力欄の決まり (本文の最大の長さ)と、自分が送れる状態かを返す。
    /// </summary>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>本文の最大の長さと、自分が受け取り先を指定しているか。</returns>
    private static async Task<IResult> LoadFormAsync(IMessageRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
        => Results.Ok(new MessageFormDto(MessageLimits.BodyMaxLength, await repository.CanReceiveAsync(currentUser.Id, cancellationToken)));

    /// <summary>
    /// 宛先に選べる人を、表示名の前方一致で返す。
    /// </summary>
    /// <param name="q">表示名の先頭の文字。無いときは、全員。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>受け取り先を指定している人。</returns>
    private static async Task<IResult> SearchPeopleAsync(string? q, IMessageRepository repository, CancellationToken cancellationToken)
    {
        var people = await repository.SearchPeopleAsync(q ?? "", SearchCount, cancellationToken);
        return Results.Ok(people.Select(person => new PersonDto(person.UserId, person.DisplayName)).ToArray());
    }

    /// <summary>
    /// メッセージを送る。メッセージと通知の状態の行を書くだけで、実際に送るのは MmmBatch。
    /// </summary>
    /// <param name="request">送る内容。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>送れたら 200 と、書いた会話・メッセージの番号。入力が正しくないときは 400。参加者でないときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> SendAsync(MessageSendDto request, IMessageRepository repository, CancellationToken cancellationToken)
    {
        if (!MessageDraft.IsValidBody(request.Body))
        {
            return Results.BadRequest(new ErrorResponse($"本文は、空でない {MessageLimits.BodyMaxLength} 文字までです。"));
        }
        if ((request.ConversationId > 0) == (request.PeerUserId > 0) || request.ReplyToMessageId < 0)
        {
            return Results.BadRequest(new ErrorResponse("送り先が正しくありません。"));
        }

        var result = await repository.SendAsync(request.ConversationId, request.PeerUserId, request.Body, request.ReplyToMessageId, cancellationToken);
        return result.Outcome switch
        {
            SendMessageOutcome.Sent => Results.Ok(new MessageSentDto(result.ConversationId, result.MessageId)),
            SendMessageOutcome.NoReceiveTarget => Results.BadRequest(new ErrorResponse(MessageDraft.NoReceiveTargetMessage)),
            SendMessageOutcome.SelfNoReceiveTarget => Results.BadRequest(new ErrorResponse(MessageDraft.SelfNoReceiveTargetMessage)),
            SendMessageOutcome.ReplyInvalid => Results.BadRequest(new ErrorResponse("返信元のメッセージが見つかりません。")),
            _ => Results.NotFound(new ErrorResponse("会話が見つかりません。")),
        };
    }

    /// <summary>
    /// 会話の更新の知らせを流す。会話を開いている間、つないだままにする。
    /// </summary>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="hub">知らせる口。</param>
    /// <param name="cancellationToken">端末が切れたときに止まるトークン。</param>
    /// <returns>Server-Sent Events の流れ。</returns>
    private static IResult Events(CurrentUser currentUser, MessageChangeHub hub, CancellationToken cancellationToken)
        => TypedResults.ServerSentEvents(hub.SubscribeAsync(currentUser.Id, cancellationToken));

    /// <summary>
    /// 読み込みの結果を、API の型にする。
    /// </summary>
    /// <param name="sync">読み込みの結果。</param>
    /// <param name="userId">自分の番号。</param>
    /// <returns>API の型。</returns>
    private static MessageSyncDto ToDto(MessageSyncResult sync, int userId)
    {
        var now = DateTimeOffset.Now;
        return new MessageSyncDto(
            userId, sync.NextBase,
            [.. sync.Conversations.Select(conversation => ToDto(conversation, now))],
            [.. sync.Messages.Select(message => ToDto(message, userId, now))]);
    }

    /// <summary>
    /// 会話の要約を、API の型にする。
    /// </summary>
    /// <param name="conversation">会話の要約。</param>
    /// <param name="now">今。時刻の表示に使う。</param>
    /// <returns>API の型。</returns>
    private static ConversationDto ToDto(ConversationSummary conversation, DateTimeOffset now)
        => new(
            conversation.Id, conversation.Kind == ConversationKind.Direct, conversation.Name, conversation.PeerUserId, conversation.PeerCanReceive,
            conversation.LastMessageId, conversation.LastText, FormatTime(conversation.LastAt, now));

    /// <summary>
    /// メッセージを、API の型にする。
    /// </summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="userId">自分の番号。</param>
    /// <param name="now">今。時刻の表示に使う。</param>
    /// <returns>API の型。</returns>
    private static MessageDto ToDto(ChatMessage message, int userId, DateTimeOffset now)
        => new(
            message.Id, message.ConversationId, message.SenderUserId, message.SenderName, message.SenderUserId == userId, message.ReplyToMessageId,
            message.ReplyToSenderName, message.ReplyToText, message.Body, message.CreatedAt, FormatTime(message.CreatedAt, now),
            message.DeliveryStatus is { } status ? (int)status : 0, message.UpdatedAt);

    /// <summary>
    /// 日時を、表示の文字にする。サーバーの時計で作る。ブラウザ上の .NET は、端末のタイムゾーンを知らないため。
    /// </summary>
    /// <param name="at">日時。</param>
    /// <param name="now">今。</param>
    /// <returns>今日なら「HH:mm」、それ以外は「M/d HH:mm」。</returns>
    private static string FormatTime(DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToLocalTime();
        return local.Date == now.Date ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("M/d HH:mm", CultureInfo.InvariantCulture);
    }
}
