using System.Globalization;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Notifications.Client;

namespace MmmToolWeb.Notifications;

/// <summary>
/// 受け取り先と、機能ごとの「通知を受け取る」の API。<c>/api/notifications</c> の下に置く。
/// </summary>
/// <remarks>
/// 通知の機能は機能番号を持たないので、管理者がメッセージやノートを無効にした人も、受け取り先を直せる。
/// 受け取り先は共通の設定で、番号は <see cref="ReceiveTarget"/> にある。「通知を受け取る」のオン・オフは、<see cref="INotificationFeatureSetting"/> に登録された機能の分だけ読み書きする。
/// </remarks>
internal static class ReceiveSettingEndpoints
{
    /// <summary>
    /// 受け取り先と「通知を受け取る」の API を登録する。
    /// </summary>
    /// <param name="notifications">登録先の <c>/api/notifications</c>。</param>
    public static void Map(RouteGroupBuilder notifications)
    {
        notifications.MapGet("/receive-channel", LoadReceiveSettingAsync);
        notifications.MapPut("/receive-channel", SaveReceiveSettingAsync);
        notifications.MapGet("/features", LoadFeaturesAsync);
        notifications.MapPut("/features", SaveFeatureAsync);
    }

    /// <summary>
    /// 受け取り先の設定を返す。自分が使用者の送信先と、選んでいる送信先。
    /// </summary>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="settings">ユーザーごとの設定の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>送信先の一覧と、選んでいる送信先の番号。送る先の値は含めない。</returns>
    private static async Task<IResult> LoadReceiveSettingAsync(
        INotificationChannelRepository channels, IAppUserSettingRepository settings, CancellationToken cancellationToken)
    {
        var list = await ListChannelsAsync(channels, cancellationToken);
        var saved = await settings.GetAsync(ReceiveTarget.FeatureNo, ReceiveTarget.CategoryNo, ReceiveTarget.CodeNo, cancellationToken);
        var selected = int.TryParse(saved, CultureInfo.InvariantCulture, out var id) && list.Any(channel => channel.Id == id) ? id : 0;
        return Results.Ok(new ReceiveSettingDto(list, selected));
    }

    /// <summary>
    /// 受け取り先を指定する。0 なら、指定を外す。
    /// </summary>
    /// <param name="request">指定する送信先。</param>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="settings">ユーザーごとの設定の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。自分が使用者でない送信先の番号は 400。</returns>
    private static async Task<IResult> SaveReceiveSettingAsync(
        ReceiveChannelRequest request, INotificationChannelRepository channels, IAppUserSettingRepository settings, CancellationToken cancellationToken)
    {
        if (request.ChannelId == 0)
        {
            await settings.ClearAsync(ReceiveTarget.FeatureNo, ReceiveTarget.CategoryNo, ReceiveTarget.CodeNo, cancellationToken);
            return Results.NoContent();
        }

        var list = await ListChannelsAsync(channels, cancellationToken);
        if (!list.Any(channel => channel.Id == request.ChannelId))
        {
            return Results.BadRequest(new ErrorResponse("選んだ送信先がありません。選び直してください。"));
        }

        await settings.SetAsync(
            ReceiveTarget.FeatureNo, ReceiveTarget.CategoryNo, ReceiveTarget.CodeNo, request.ChannelId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// 「通知を受け取る」のオン・オフを持つ機能と、自分の設定を返す。
    /// </summary>
    /// <param name="features">登録された機能。</param>
    /// <param name="settings">ユーザーごとの設定の保存先。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="access">機能の利用。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>機能ごとのオン・オフ。管理者が無効にした機能は含めない。</returns>
    private static async Task<IResult> LoadFeaturesAsync(
        IEnumerable<INotificationFeatureSetting> features, IAppUserSettingRepository settings, CurrentUser currentUser, FeatureAccessService access,
        CancellationToken cancellationToken)
    {
        var states = await access.GetAllAsync(currentUser.Id, cancellationToken);
        var values = await settings.GetByFeatureAsync(NotificationFeatureSettings.CategoryNo, NotificationFeatureSettings.CodeNo, cancellationToken);
        var list = new List<NotificationFeatureDto>();
        foreach (var feature in features.Where(feature => !states.TryGetValue(feature.FeatureNo, out var state) || state != FeatureAccess.Disabled))
        {
            values.TryGetValue(feature.FeatureNo, out var saved);
            list.Add(new NotificationFeatureDto(feature.FeatureNo, feature.Name, NotificationFeatureSettings.IsOn(saved, feature.DefaultOn)));
        }
        return Results.Ok(list.ToArray());
    }

    /// <summary>
    /// 機能の「通知を受け取る」のオン・オフを保存する。
    /// </summary>
    /// <param name="request">機能番号とオン・オフ。</param>
    /// <param name="features">登録された機能。</param>
    /// <param name="settings">ユーザーごとの設定の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。登録されていない機能番号は 400。</returns>
    private static async Task<IResult> SaveFeatureAsync(
        NotificationFeatureRequest request, IEnumerable<INotificationFeatureSetting> features, IAppUserSettingRepository settings, CancellationToken cancellationToken)
    {
        if (!features.Any(feature => feature.FeatureNo == request.FeatureNo))
        {
            return Results.BadRequest(new ErrorResponse("この機能の通知の設定はありません。画面を開き直してください。"));
        }

        await settings.SetAsync(
            request.FeatureNo, NotificationFeatureSettings.CategoryNo, NotificationFeatureSettings.CodeNo, NotificationFeatureSettings.ToValue(request.IsOn), cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// 自分が使用者の送信先を、種類ごとに読んで並べる。
    /// </summary>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>送信先。種類の順で、番号の順。</returns>
    private static async Task<IReadOnlyList<ReceiveChannelDto>> ListChannelsAsync(INotificationChannelRepository channels, CancellationToken cancellationToken)
    {
        var byKind = await channels.GetAllSummariesAsync(cancellationToken);
        var list = new List<ReceiveChannelDto>();
        foreach (var kind in Enum.GetValues<NotificationChannelKind>())
        {
            if (byKind.TryGetValue(kind, out var summaries))
            {
                list.AddRange(summaries.OrderBy(summary => summary.Id).Select(summary => new ReceiveChannelDto(summary.Id, summary.Name, NotificationChannelKinds.ToText(kind))));
            }
        }
        return list;
    }
}
