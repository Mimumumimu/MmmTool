using System.Globalization;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Notifications.Client;

namespace MmmToolWeb.Notifications;

/// <summary>通知先と機能ごとの「通知を受け取る」の API</summary>
/// <remarks>
/// /api/notifications の下に置く。
/// 通知の機能は機能番号を持たないので、管理者がメッセージやノートを無効にした人も、通知先を直せる。
/// 通知先は共通の設定で、番号は <see cref="NotificationTarget"/> にある。
/// 「通知を受け取る」のオン・オフは <see cref="INotificationFeatureSetting"/> に登録された機能の分だけ読み書きする。
/// </remarks>
internal static class NotificationTargetSettingEndpoints
{
    /// <summary>通知先と「通知を受け取る」の API を登録する</summary>
    /// <param name="notifications">登録先の <c>/api/notifications</c></param>
    public static void Map(RouteGroupBuilder notifications)
    {
        notifications.MapGet("/notification-channel", LoadNotificationTargetSettingAsync);
        notifications.MapPut("/notification-channel", SaveNotificationTargetSettingAsync);
        notifications.MapGet("/features", LoadFeaturesAsync);
        notifications.MapPut("/features", SaveFeatureAsync);
    }

    /// <summary>通知先の設定を返す</summary>
    /// <param name="channels">送信先の保存先</param>
    /// <param name="settings">ユーザーごとの設定の保存先</param>
    /// <param name="cancellationToken">キャンセルの通知</param>
    /// <returns>
    /// 送信先の一覧と、選んでいる送信先の番号。
    /// 送る先の値は含めない。
    /// 選んでいる送信先が一覧に無いときは 0。
    /// </returns>
    /// <remarks>自分が使用者の Web 版通知の送信先と選んでいる送信先</remarks>
    private static async Task<IResult> LoadNotificationTargetSettingAsync(
        INotificationChannelRepository channels, IAppUserSettingRepository settings, CancellationToken cancellationToken)
    {
        var list = await ListChannelsAsync(channels, cancellationToken);
        var saved = await settings.GetAsync(NotificationTarget.FeatureNo, NotificationTarget.CategoryNo, NotificationTarget.CodeNo, cancellationToken);
        var selected = int.TryParse(saved, CultureInfo.InvariantCulture, out var id) && list.Any(channel => channel.Id == id) ? id : 0;
        return Results.Ok(new NotificationTargetSettingDto(list, selected));
    }

    /// <summary>通知先を指定する</summary>
    /// <param name="request">指定する送信先</param>
    /// <param name="channels">送信先の保存先</param>
    /// <param name="settings">ユーザーごとの設定の保存先</param>
    /// <param name="cancellationToken">キャンセルの通知</param>
    /// <returns>
    /// 成功なら 204
    /// 自分が使用者でない送信先の番号は 400
    /// </returns>
    /// <remarks>0 なら、指定を外す。</remarks>
    private static async Task<IResult> SaveNotificationTargetSettingAsync(
        NotificationChannelRequest request, INotificationChannelRepository channels, IAppUserSettingRepository settings, CancellationToken cancellationToken)
    {
        if (request.ChannelId == 0)
        {
            await settings.ClearAsync(NotificationTarget.FeatureNo, NotificationTarget.CategoryNo, NotificationTarget.CodeNo, cancellationToken);
            return Results.NoContent();
        }

        var list = await ListChannelsAsync(channels, cancellationToken);
        if (!list.Any(channel => channel.Id == request.ChannelId))
        {
            return Results.BadRequest(new ErrorResponse("選んだ送信先がありません。選び直してください。"));
        }

        await settings.SetAsync(
            NotificationTarget.FeatureNo, NotificationTarget.CategoryNo, NotificationTarget.CodeNo, request.ChannelId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return Results.NoContent();
    }

    /// <summary>「通知を受け取る」のオン・オフを持つ機能と自分の設定を返す</summary>
    /// <param name="features">登録された機能</param>
    /// <param name="settings">ユーザーごとの設定の保存先</param>
    /// <param name="currentUser">今のユーザー</param>
    /// <param name="access">機能の利用</param>
    /// <param name="cancellationToken">キャンセルの通知</param>
    /// <returns>
    /// 機能ごとのオン・オフ
    /// 管理者が無効にした機能は含めない
    /// </returns>
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

    /// <summary>機能の「通知を受け取る」のオン・オフを保存する</summary>
    /// <param name="request">機能番号とオン・オフ</param>
    /// <param name="features">登録された機能</param>
    /// <param name="settings">ユーザーごとの設定の保存先</param>
    /// <param name="cancellationToken">キャンセルの通知</param>
    /// <returns>
    /// 成功なら 204
    /// 登録されていない機能番号は 400
    /// </returns>
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

    /// <summary>通知先に選べる送信先を読んで並べる</summary>
    /// <param name="channels">送信先の保存先</param>
    /// <param name="cancellationToken">キャンセルの通知</param>
    /// <returns>
    /// 送信先
    /// 番号の順
    /// </returns>
    /// <remarks>
    /// 自分が使用者の送信先のうち、種類が Web 版通知のものだけ。
    /// ntfy と Discord は通知先に選べない。
    /// PC には通知ダイアログがあるので、通知先はスマホの通知のためだけにある。
    /// 保存の検査も、この一覧を使うので、選べない種類の番号は断る。
    /// </remarks>
    private static async Task<IReadOnlyList<NotificationChannelDto>> ListChannelsAsync(INotificationChannelRepository channels, CancellationToken cancellationToken)
    {
        var summaries = await channels.GetSummariesAsync(NotificationChannelKind.WebPush, cancellationToken);
        var kindText = NotificationChannelKinds.ToText(NotificationChannelKind.WebPush);
        return [.. summaries.OrderBy(summary => summary.Id).Select(summary => new NotificationChannelDto(summary.Id, summary.Name, kindText))];
    }
}
