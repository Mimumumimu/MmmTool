using System.Globalization;
using Microsoft.Extensions.Configuration;
using MmmTool.NotificationChannels.Core;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Notifications.Client;

namespace MmmToolWeb.Notifications;

/// <summary>
/// 通知の API <c>/api/notifications</c>。
/// </summary>
internal static class NotificationEndpoints
{
    /// <summary>送り手の公開鍵を置く設定のキー。MmmBatch が作った公開鍵を写す。</summary>
    internal const string PublicKeySetting = "Web:PushPublicKey";

    /// <summary>受け取った通知の一覧に出す、最大の件数。種類ごとにも、この件数まで読む。</summary>
    private const int ReceivedListCount = 50;

    /// <summary>
    /// 通知の API を登録する。
    /// </summary>
    /// <param name="notifications">登録先の <c>/api/notifications</c>。</param>
    public static void Map(RouteGroupBuilder notifications)
    {
        notifications.MapGet("/settings", LoadSettingsAsync);
        notifications.MapGet("/received", LoadReceivedAsync);
        notifications.MapGet("/destination", LoadDestinationAsync);
        notifications.MapPost("/subscriptions", RegisterAsync);
        notifications.MapPost("/subscriptions/remove", RemoveAsync);
    }

    /// <summary>
    /// 画面を出すのに要る情報を返す。
    /// </summary>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="configuration">サーバーの設定。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>送り手の公開鍵と、自分が登録した端末。端末の宛先は含めない。</returns>
    private static async Task<IResult> LoadSettingsAsync(
        INotificationChannelRepository channels, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var devices = await channels.GetSummariesAsync(NotificationChannelKind.WebPush, cancellationToken);
        var publicKey = configuration[PublicKeySetting];
        return Results.Ok(new PushSettingsResponse(
            string.IsNullOrWhiteSpace(publicKey) ? null : publicKey.Trim(),
            [.. devices.Select(device => new PushDeviceDto(device.Id, device.Name))]));
    }

    /// <summary>
    /// 受け取った通知の一覧を返す。種類ごとの提供元から集めて、新しい順に並べる。
    /// </summary>
    /// <param name="item">通知を押して開いたときの、その通知の識別子。一覧に無ければ、その 1 件も加える。</param>
    /// <param name="sources">受け取った通知の提供元。登録された種類のぶん。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>新しい順の通知。提供元の名前を、ここでは持たない。</returns>
    private static async Task<IResult> LoadReceivedAsync(
        string? item, IEnumerable<IReceivedNotificationSource> sources, CancellationToken cancellationToken)
    {
        var sourceList = sources.ToList();
        var all = new List<(IReceivedNotificationSource Source, ReceivedNotification Notification)>();
        foreach (var source in sourceList)
        {
            var latest = await source.GetLatestAsync(ReceivedListCount, cancellationToken);
            all.AddRange(latest.Select(notification => (source, notification)));
        }

        // 種類ごとに最新の件数まで読んでいるので、全部を新しい順に並べて、最新の件数だけを残す
        var entries = all.OrderByDescending(entry => entry.Notification.ReceivedAt).Take(ReceivedListCount).ToList();

        // 押して開いた通知が、一覧に入らないほど古いときは、その 1 件を加える
        if (TryParseKey(item, out var kind, out var id)
            && !entries.Any(entry => entry.Source.Key == kind && entry.Notification.Id == id)
            && sourceList.FirstOrDefault(source => source.Key == kind) is { } owner
            && await owner.GetAsync(id, cancellationToken) is { } opened)
        {
            entries.Add((owner, opened));
        }

        var now = DateTimeOffset.Now;
        var items = entries
            .OrderByDescending(entry => entry.Notification.ReceivedAt)
            .Select(entry => new ReceivedNotificationDto(
                $"{entry.Source.Key}:{entry.Notification.Id}",
                entry.Source.KindName,
                entry.Notification.Title,
                entry.Notification.Body,
                entry.Notification.Link,
                entry.Notification.ReceivedAt,
                FormatReceivedAt(entry.Notification.ReceivedAt, now)))
            .ToList();
        return Results.Ok(new ReceivedListResponse(items));
    }

    /// <summary>
    /// 通知を押したときに開く画面を返す。種類ごとの行き先の登録に聞く。
    /// </summary>
    /// <param name="item">通知の識別子。「種類:番号」の形。</param>
    /// <param name="destinations">行き先の登録。登録された種類のぶん。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>開く画面の道。行き先が無い種類・開けないときは、道が null。</returns>
    /// <remarks>メッセージなどの名前は、ここでは持たない。登録が無い種類は、通知の一覧を開く。</remarks>
    private static async Task<IResult> LoadDestinationAsync(
        string? item, IEnumerable<INotificationDestination> destinations, CancellationToken cancellationToken)
    {
        if (!TryParseKey(item, out var kind, out var id)
            || destinations.FirstOrDefault(destination => destination.Key == kind) is not { } owner)
        {
            return Results.Ok(new DestinationResponse(null));
        }

        return Results.Ok(new DestinationResponse(await owner.GetPathAsync(id, cancellationToken)));
    }

    /// <summary>
    /// 識別子を、種類と番号に分ける。
    /// </summary>
    /// <param name="key">「種類:番号」の形の識別子。</param>
    /// <param name="kind">種類のキー。</param>
    /// <param name="id">番号。</param>
    /// <returns>分けられたら true。</returns>
    private static bool TryParseKey(string? key, out string kind, out int id)
    {
        kind = "";
        id = 0;
        var parts = key?.Split(':');
        if (parts is not [{ Length: > 0 } kindPart, var idPart] || !int.TryParse(idPart, out id))
        {
            return false;
        }
        kind = kindPart;
        return true;
    }

    /// <summary>
    /// 届いた日時を、表示の文字にする。サーバーの時計で作る。ブラウザ上の .NET は、端末のタイムゾーンを知らないため。
    /// </summary>
    /// <param name="receivedAt">届いた日時。</param>
    /// <param name="now">今。</param>
    /// <returns>今日なら「HH:mm」、それ以外は「M/d HH:mm」。</returns>
    private static string FormatReceivedAt(DateTimeOffset receivedAt, DateTimeOffset now)
    {
        var local = receivedAt.ToLocalTime();
        return local.Date == now.Date ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("M/d HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// この端末の宛先を登録する。この端末が登録した行があれば更新し、無ければ追加する。
    /// </summary>
    /// <param name="request">登録する内容。</param>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>登録した送信先の番号。入力が正しくないときは 400。</returns>
    /// <remarks>
    /// 状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。
    /// 更新は、自分の、Web 版通知の行だけが対象。ほかの種類の行や、ほかの人の行は、番号を偽られても書き換わらない。
    /// 更新した行が削除済みなら、削除を戻す。端末が登録し直したので、通知を受け取りたいという意思があるため。
    /// </remarks>
    private static async Task<IResult> RegisterAsync(
        PushRegisterRequest request, INotificationChannelRepository channels, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > NotificationChannelRules.NameMaxLength)
        {
            return Results.BadRequest(new ErrorResponse("端末の名前が正しくありません。"));
        }

        var subscription = new WebPushSubscription(request.Endpoint, request.P256dh, request.Auth);
        if (!subscription.IsValid)
        {
            return Results.BadRequest(new ErrorResponse("この端末の通知の宛先を受け付けられませんでした。"));
        }

        var value = subscription.ToValue();
        if (value.Length > NotificationChannelRules.ValueMaxLength)
        {
            return Results.BadRequest(new ErrorResponse("この端末の通知の宛先が長すぎて、登録できませんでした。"));
        }

        var channel = new NotificationChannel { Kind = NotificationChannelKind.WebPush, Name = name, Value = value };
        if (request.Id > 0 && await channels.UpdateAsync(channel with { Id = request.Id }, cancellationToken))
        {
            // MmmTool の送信先の一覧で削除された行でも、更新は成功する。登録し直す操作なので、削除を戻す。戻さないと、届かない
            await channels.SetDeletedAsync(request.Id, isDeleted: false, cancellationToken);
            return Results.Ok(new PushRegisterResponse(request.Id));
        }

        // 覚えていた番号が、削除された・自分の行ではなかったときは、新しく追加する
        var added = await channels.AddAsync(channel, cancellationToken);
        return Results.Ok(new PushRegisterResponse(added.Id));
    }

    /// <summary>
    /// この端末の登録を解除する。論理削除する。
    /// </summary>
    /// <param name="request">解除する送信先。</param>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。対象が無い・自分の行ではないときは 404。</returns>
    /// <remarks>対象は、自分が使用者の行だけ。ほかの人の行は、番号を偽られても削除されない。</remarks>
    private static async Task<IResult> RemoveAsync(
        PushRemoveRequest request, INotificationChannelRepository channels, CancellationToken cancellationToken)
    {
        return await channels.SetDeletedAsync(request.Id, isDeleted: true, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new ErrorResponse("登録が見つかりません。"));
    }
}
