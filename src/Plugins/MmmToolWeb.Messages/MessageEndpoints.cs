using System.Globalization;
using MmmSdk.Core.Components.TextLinks;
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
        messages.MapPost("/conversations/{conversationId:int}/members", AddMembersAsync);
        messages.MapPut("/conversations/{conversationId:int}/name", RenameGroupAsync);
        messages.MapPost("/conversations/{conversationId:int}/leave", LeaveGroupAsync);
        messages.MapPut("/conversations/{conversationId:int}/read", MarkReadAsync);
        messages.MapGet("/unread-count", UnreadCountAsync);
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
    /// 入力欄の決まり (本文の最大の長さ)と、自分と相手が送れる状態かを返す。
    /// </summary>
    /// <param name="conversationId">開いている会話の番号。サーバーが、会話の参加者であることを確かめて、相手を見つける。省略か 0 のときは、使わない。</param>
    /// <param name="peerUserId">まだ会話が無いときの、1 対 1 の相手の番号。会話の番号があるときは、使わない。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>本文の最大の長さと、自分と相手が受け取り先を指定しているか。自分が入っていない会話は 404。</returns>
    /// <remarks>相手の状態は、会話の要約の控えが古くなるので、要るたびに DB から読む。</remarks>
    private static async Task<IResult> LoadFormAsync(
        int? conversationId, int? peerUserId, IMessageRepository repository, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        bool peerCanReceive;
        if (conversationId is > 0)
        {
            if (await repository.CanPeerReceiveAsync(conversationId.Value, cancellationToken) is not { } canReceive)
            {
                return Results.NotFound(new ErrorResponse("会話が見つかりません。"));
            }
            peerCanReceive = canReceive;
        }
        else
        {
            peerCanReceive = peerUserId is not > 0 || await repository.CanReceiveAsync(peerUserId.Value, cancellationToken);
        }

        return Results.Ok(new MessageFormDto(
            MessageLimits.BodyMaxLength, await repository.CanReceiveAsync(currentUser.Id, cancellationToken), peerCanReceive,
            MessageLimits.GroupNameMaxLength, MessageLimits.GroupMaxMembers));
    }

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
        var groupMembers = request.GroupMemberIds ?? [];
        var isNewGroup = !string.IsNullOrWhiteSpace(request.GroupName);
        if (new[] { request.ConversationId > 0, request.PeerUserId > 0, isNewGroup }.Count(chosen => chosen) != 1
            || request.ReplyToMessageId < 0 || (!isNewGroup && groupMembers.Count > 0))
        {
            return Results.BadRequest(new ErrorResponse("送り先が正しくありません。"));
        }

        var target = request.ConversationId > 0 ? MessageTarget.Existing(request.ConversationId)
            : request.PeerUserId > 0 ? MessageTarget.Direct(request.PeerUserId)
            : MessageTarget.NewGroup(request.GroupName, groupMembers);
        var result = await repository.SendAsync(target, request.Body, request.ReplyToMessageId, cancellationToken);
        return result.Outcome switch
        {
            SendMessageOutcome.Sent => Results.Ok(new MessageSentDto(result.ConversationId, result.MessageId)),
            SendMessageOutcome.NoReceiveTarget => Results.BadRequest(new ErrorResponse(MessageDraft.NoReceiveTargetMessage)),
            SendMessageOutcome.SelfNoReceiveTarget => Results.BadRequest(new ErrorResponse(MessageDraft.SelfNoReceiveTargetMessage)),
            SendMessageOutcome.ReplyInvalid => Results.BadRequest(new ErrorResponse("返信元のメッセージが見つかりません。")),
            SendMessageOutcome.GroupInvalid => Results.BadRequest(new ErrorResponse("グループの名前か参加者が正しくありません。")),
            _ => Results.NotFound(new ErrorResponse("会話が見つかりません。")),
        };
    }

    /// <summary>
    /// グループに人を追加する。
    /// </summary>
    /// <param name="conversationId">グループの番号。</param>
    /// <param name="request">追加する人の番号。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>追加できたときは 204。入力が正しくないときは 400。参加者でないときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> AddMembersAsync(int conversationId, GroupMembersAddDto request, IMessageRepository repository, CancellationToken cancellationToken)
    {
        if (request.UserIds is not { Count: > 0 } || request.UserIds.Any(userId => userId < 1))
        {
            return Results.BadRequest(new ErrorResponse("追加する人が正しくありません。"));
        }

        return ToResult(await repository.AddMembersAsync(conversationId, request.UserIds, cancellationToken), "追加する人が正しくありません。");
    }

    /// <summary>
    /// グループの名前を変える。
    /// </summary>
    /// <param name="conversationId">グループの番号。</param>
    /// <param name="request">新しい名前。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>変えられたときは 204。名前が正しくないときは 400。参加者でないときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> RenameGroupAsync(int conversationId, GroupRenameDto request, IMessageRepository repository, CancellationToken cancellationToken)
        => ToResult(await repository.RenameGroupAsync(conversationId, request.Name ?? "", cancellationToken), "グループの名前が正しくありません。");

    /// <summary>
    /// グループを抜ける。
    /// </summary>
    /// <param name="conversationId">グループの番号。</param>
    /// <param name="request">抜ける要求。中身は無いが、JSON の本文を要る形にする。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>抜けられたときは 204。1 対 1 は 400。参加者でないときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> LeaveGroupAsync(int conversationId, GroupLeaveDto request, IMessageRepository repository, CancellationToken cancellationToken)
        => ToResult(await repository.LeaveGroupAsync(conversationId, cancellationToken), "グループではない会話は抜けられません。");

    /// <summary>
    /// グループの変更の結果を、応答にする。
    /// </summary>
    /// <param name="outcome">結果の区分。</param>
    /// <param name="invalidMessage">入力が正しくないときの理由。</param>
    /// <returns>変えられたときは 204、グループでないか入力が正しくないときは 400、参加者でないときは 404。</returns>
    private static IResult ToResult(GroupChangeOutcome outcome, string invalidMessage)
        => outcome switch
        {
            GroupChangeOutcome.Done => Results.NoContent(),
            GroupChangeOutcome.NotGroup or GroupChangeOutcome.Invalid => Results.BadRequest(new ErrorResponse(invalidMessage)),
            _ => Results.NotFound(new ErrorResponse("会話が見つかりません。")),
        };

    /// <summary>
    /// 会話の、自分が読んだ位置を書く。相手の画面に「既読」が出る元になる。
    /// </summary>
    /// <param name="conversationId">会話の番号。</param>
    /// <param name="request">読んだ最後のメッセージの番号。</param>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>書いたときと、読んだ位置がすでにその番号以上だったときは 204。番号がその会話のメッセージでないときは 400。参加者でないときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> MarkReadAsync(int conversationId, MessageReadDto request, IMessageRepository repository, CancellationToken cancellationToken)
    {
        if (conversationId < 1 || request.MessageId < 1)
        {
            return Results.BadRequest(new ErrorResponse("読んだ位置が正しくありません。"));
        }

        return await repository.MarkReadAsync(conversationId, request.MessageId, cancellationToken) switch
        {
            MarkReadOutcome.Advanced or MarkReadOutcome.Unchanged => Results.NoContent(),
            MarkReadOutcome.MessageInvalid => Results.BadRequest(new ErrorResponse("読んだ位置が正しくありません。")),
            _ => Results.NotFound(new ErrorResponse("会話が見つかりません。")),
        };
    }

    /// <summary>
    /// 読んでいないメッセージの、全部の会話の合計を返す。ホーム画面のアイコンの数字とタイルの数に使う。
    /// </summary>
    /// <param name="repository">会話とメッセージの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>読んでいないメッセージの数。</returns>
    private static async Task<IResult> UnreadCountAsync(IMessageRepository repository, CancellationToken cancellationToken)
        => Results.Ok(new UnreadCountDto(await repository.GetUnreadCountAsync(cancellationToken)));

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
            [.. sync.Messages.Select(message => ToDto(message, userId, now))],
            sync.LeftConversationIds);
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
            conversation.LastMessageId, conversation.LastText, FormatTime(conversation.LastAt, now), conversation.UnreadCount,
            [.. conversation.Readers.Select(reader => new ReadPositionDto(reader.UserId, reader.LastReadMessageId, reader.DisplayName))],
            conversation.LastSenderUserId, conversation.LastSenderName);

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
            message.UpdatedAt,
            [.. TextLinkFinder.Find(message.Body).Select(link => new MessageLinkDto(link.Start, link.Length, link.Href))]);

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
