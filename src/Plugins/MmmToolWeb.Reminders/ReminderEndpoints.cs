using System.Globalization;
using MmmSdk.Core.Components.DaysOfWeek;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.NotificationChannels.Core;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Reminders.Client;

namespace MmmToolWeb.Reminders;

/// <summary>
/// リマインダーの API <c>/api/reminders</c>。
/// </summary>
/// <remarks>
/// 状態 (未・スヌーズ・完了)は、読むだけで、書かない。変えるのは PC 版だけ。
/// 日付の組み立てと判定は、PC 版と同じ <c>MmmTool.Reminders.Core</c> を使う。ブラウザの発行物に <c>.Core</c> を入れないため、画面側には、押せる条件だけの同じ決まりがあり、ここでも確かめて、守れなければ 400 を返す。
/// </remarks>
internal static class ReminderEndpoints
{
    /// <summary>リマインダーが見つからないときの理由。</summary>
    private const string NotFoundMessage = "このリマインダーは、見つからないか、ほかの人が先に変えました。";

    /// <summary>
    /// リマインダーの API を登録する。
    /// </summary>
    /// <param name="reminders">登録先の <c>/api/reminders</c>。</param>
    public static void Map(RouteGroupBuilder reminders)
    {
        reminders.MapGet("/today", LoadTodayAsync);
        reminders.MapGet("/", LoadListAsync);
        reminders.MapGet("/options", LoadOptionsAsync);
        reminders.MapPost("/this-device", FindThisDeviceAsync);
        reminders.MapGet("/{no:int}", LoadDetailAsync);
        reminders.MapPost("/", AddAsync);
        reminders.MapPut("/{no:int}", UpdateAsync);
        reminders.MapPost("/{no:int}/deleted", DeleteAsync);
    }

    /// <summary>
    /// ある日の対象と、その日の対応状態を返す。
    /// </summary>
    /// <param name="day">日。ブラウザの時計の今日。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>その日の対象。時刻、番号の順。</returns>
    private static async Task<IResult> LoadTodayAsync(DateOnly day, SqlServerReminderWebRepository repository, CancellationToken cancellationToken)
    {
        var targets = await repository.GetTodayAsync(day, cancellationToken);
        return Results.Ok(new ReminderTodayResponse([.. targets.Select(target => new ReminderTodayItemDto(
            target.Reminder.No,
            TimeText(target.Reminder.Time),
            target.Reminder.Title,
            target.Reminder.Note,
            SafeLink(target.Reminder.Link),
            (int)target.Status))]));
    }

    /// <summary>
    /// 見えるリマインダーの一覧を返す。
    /// </summary>
    /// <param name="day">今日。ブラウザの時計の今日で、「過去の予定」を外す基準。</param>
    /// <param name="includePast">日付が過去の日付指定も含めるか。</param>
    /// <param name="includeDeleted">削除済みも含めるか。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>日付、時刻、番号の順の一覧。曜日指定は後ろ。</returns>
    private static async Task<IResult> LoadListAsync(
        DateOnly day, bool? includePast, bool? includeDeleted, SqlServerReminderWebRepository repository, CancellationToken cancellationToken)
    {
        var list = await repository.GetListAsync(day, includePast ?? false, includeDeleted ?? false, cancellationToken);
        return Results.Ok(new ReminderListResponse([.. list.Select(reminder => new ReminderListItemDto(
            reminder.No,
            WhenText(reminder),
            TimeText(reminder.Time),
            reminder.Title,
            reminder.IsDeleted))]));
    }

    /// <summary>
    /// 入力のシートが要る、宛先と送信先の選択肢と、文字数の上限を返す。
    /// </summary>
    /// <param name="target">編集するリマインダーの宛先の人の番号。もう選べない人のときも、選択肢に含めるため。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="users">ユーザーの保存先。</param>
    /// <param name="channels">送信先の保存先。送る先の値は読まない。</param>
    /// <param name="time">現在時刻の取得元。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>選択肢と上限。宛先は、先頭が自分、次が全員、そのあとはほかの使える人。</returns>
    private static async Task<IResult> LoadOptionsAsync(
        int? target,
        SqlServerReminderWebRepository repository,
        IAppUserRepository users,
        INotificationChannelRepository channels,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var selfId = repository.CurrentUserId;
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        // ユーザーの一覧と、リマインダーを無効にされた人を、1 回の問い合わせで読む
        var states = await users.GetUsersWithFeatureStateAsync(CodeFeatures.Reminders, cancellationToken);
        var targets = new List<ReminderTargetOptionDto> { new(selfId, "自分"), new(0, "全員") };
        targets.AddRange(states
            .Where(state => state.User.Id != selfId && state.User.IsActiveOn(today) && !state.IsFeatureDisabled)
            .OrderBy(state => state.User.DisplayName, StringComparer.CurrentCulture)
            .Select(state => new ReminderTargetOptionDto(state.User.Id, state.User.DisplayName)));

        // 編集で、宛先がもう選べない人 (削除済み・期間外・無効)のときも、保存で宛先が変わらないよう、選択肢に足す
        if (target is { } included && targets.All(option => option.UserId != included))
        {
            var name = states.FirstOrDefault(state => state.User.Id == included)?.User.DisplayName ?? $"ユーザー {included}";
            targets.Add(new ReminderTargetOptionDto(included, name));
        }

        var mine = await channels.GetAllSummariesAsync(cancellationToken);
        var options = mine
            .SelectMany(pair => pair.Value.Select(summary => new ReminderChannelOptionDto(summary.Id, $"{summary.Name} ({NotificationChannelKinds.ToText(pair.Key)})")))
            .OrderBy(option => option.Label, StringComparer.CurrentCulture);

        return Results.Ok(new ReminderOptionsResponse(
            targets, [.. options], ReminderLimits.TitleMaxLength, ReminderLimits.NoteMaxLength, ReminderLimits.LinkMaxLength));
    }

    /// <summary>
    /// この端末が登録した、自分の送信先の番号を返す。新規の入力で、この端末の送信先を最初から選ぶために使う。
    /// </summary>
    /// <param name="request">この端末の通知の宛先。</param>
    /// <param name="channels">送信先の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>削除されていない自分の送信先が見つかれば、その番号。無いときは 0。宛先の形が正しくないときは 400。</returns>
    /// <remarks>宛先は秘密なので、道には載せず、本文で受ける。返すのは番号だけで、宛先は返さない。</remarks>
    private static async Task<IResult> FindThisDeviceAsync(
        ReminderDeviceRequest request, INotificationChannelRepository channels, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > NotificationChannelRules.ValueMaxLength)
        {
            return Results.BadRequest(new ErrorResponse(InvalidMessage));
        }

        var found = await channels.FindWebPushAsync(request.Endpoint, cancellationToken);
        return Results.Ok(new ReminderDeviceResponse(found is { IsDeleted: false } ? found.Id : 0));
    }

    /// <summary>
    /// 編集のために、リマインダー 1 件の中身を返す。
    /// </summary>
    /// <param name="no">リマインダーの番号。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="sendSettings">送信設定の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>中身。削除済みも返す。無い・見えないときは 404。</returns>
    private static async Task<IResult> LoadDetailAsync(
        int no, SqlServerReminderWebRepository repository, SqlServerReminderSendSettingRepository sendSettings, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(no, cancellationToken) is not { } reminder)
        {
            return Results.NotFound(new ErrorResponse(NotFoundMessage));
        }

        var channelIds = await sendSettings.GetChannelIdsOfAsync(no, cancellationToken);
        return Results.Ok(new ReminderDetailDto(
            reminder.No,
            reminder.IsDeleted,
            ReminderDates.ToDate(reminder.Date),
            (int)reminder.Weekdays,
            TimeText(reminder.Time),
            reminder.Title,
            reminder.Note ?? "",
            reminder.Link ?? "",
            reminder.IsSpeak,
            reminder.TargetUserId,
            channelIds));
    }

    /// <summary>
    /// リマインダーを追加する。
    /// </summary>
    /// <param name="request">保存する内容。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="sendSettings">送信設定の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 200 と、採番した番号。入力が正しくないときは 400。リマインダーは保存できたが送信先を保存できなかったときは、200 と、番号と理由。</returns>
    /// <remarks>
    /// リマインダーと送信設定は、別の書き込み。送信設定だけ失敗したとき、画面が保存し直すと、同じリマインダーが重なって作られる。
    /// そのため、失敗を 200 の理由で返し、画面が、返した番号で更新し直せるようにする。
    /// </remarks>
    private static async Task<IResult> AddAsync(
        ReminderSaveRequest request, SqlServerReminderWebRepository repository, SqlServerReminderSendSettingRepository sendSettings, CancellationToken cancellationToken)
    {
        var error = Validate(request);
        var reminder = error is null ? ToReminder(request, 0) : null;
        if (reminder is null)
        {
            return Results.BadRequest(new ErrorResponse(error ?? InvalidMessage));
        }

        var saved = await repository.AddAsync(reminder, cancellationToken);

        // 送信先を選んだときだけ、送信設定を書く。保存した時刻が、送信設定の更新日時になる
        var channelIds = EffectiveChannelIds(request, repository.CurrentUserId);
        if (channelIds.Count > 0)
        {
            try
            {
                await sendSettings.SetChannelsAsync(saved.No, channelIds, cancellationToken);
            }
            catch (DataFileException)
            {
                return Results.Ok(new ReminderSaveResult(saved.No, SendSettingWarning));
            }
        }
        return Results.Ok(new ReminderSaveResult(saved.No));
    }

    /// <summary>
    /// リマインダーを変更する。
    /// </summary>
    /// <param name="no">リマインダーの番号。</param>
    /// <param name="request">保存する内容。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="sendSettings">送信設定の保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 200 と、番号。入力が正しくないときは 400。見えない・無いときは 404。</returns>
    private static async Task<IResult> UpdateAsync(
        int no, ReminderSaveRequest request, SqlServerReminderWebRepository repository, SqlServerReminderSendSettingRepository sendSettings, CancellationToken cancellationToken)
    {
        var error = Validate(request);
        var reminder = error is null ? ToReminder(request, no) : null;
        if (reminder is null)
        {
            return Results.BadRequest(new ErrorResponse(error ?? InvalidMessage));
        }

        if (!await repository.UpdateAsync(reminder, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse(NotFoundMessage));
        }

        // 選び直しを反映するため、送信先を選んでいなくても書く。選んでいない送信先の設定は、論理削除される。失敗しても、同じ内容で保存し直せる
        await sendSettings.SetChannelsAsync(no, EffectiveChannelIds(request, repository.CurrentUserId), cancellationToken);
        return Results.Ok(new ReminderSaveResult(no));
    }

    /// <summary>
    /// リマインダーを削除する。
    /// </summary>
    /// <param name="no">リマインダーの番号。</param>
    /// <param name="request">削除する本文。</param>
    /// <param name="repository">リマインダーの保存先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。見えない・無いときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。削除を取り消すのは、編集して保存したとき。</remarks>
    private static async Task<IResult> DeleteAsync(
        int no, ReminderDeletedRequest request, SqlServerReminderWebRepository repository, CancellationToken cancellationToken)
    {
        if (!request.IsDeleted)
        {
            return Results.BadRequest(new ErrorResponse(InvalidMessage));
        }

        return await repository.SetDeletedAsync(no, isDeleted: true, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new ErrorResponse(NotFoundMessage));
    }

    /// <summary>入力が正しくないときの理由。</summary>
    private const string InvalidMessage = "入力の内容を確かめてください。";

    /// <summary>リマインダーは保存したが、送信先を保存できなかったときの理由。</summary>
    private const string SendSettingWarning = "リマインダーは保存しましたが、送信先を保存できませんでした。もう一度、保存してください。";

    /// <summary>
    /// 要求の入力を確かめる。
    /// </summary>
    /// <param name="request">保存する内容。</param>
    /// <returns>正しくないときは画面に出せる理由。正しければ null。</returns>
    private static string? Validate(ReminderSaveRequest request)
    {
        // JSON で項目を省かれると、null が入る。型の上は null にならないが、確かめて 400 にする
        if (request.Title is null || request.Note is null || request.Link is null || request.Time is null || request.ChannelIds is null)
        {
            return InvalidMessage;
        }
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "件名を入力してください。";
        }
        if (request.Title.Length > ReminderLimits.TitleMaxLength
            || request.Note.Length > ReminderLimits.NoteMaxLength
            || request.Link.Length > ReminderLimits.LinkMaxLength)
        {
            return $"件名は {ReminderLimits.TitleMaxLength} 文字、備考は {ReminderLimits.NoteMaxLength} 文字、リンクは {ReminderLimits.LinkMaxLength} 文字までです。";
        }
        if (request.TargetUserId < 0 || request.ChannelIds.Any(id => id <= 0))
        {
            return InvalidMessage;
        }
        return null;
    }

    /// <summary>
    /// 要求を、リマインダーにする。
    /// </summary>
    /// <param name="request">保存する内容。</param>
    /// <param name="no">リマインダーの番号。新規は 0。</param>
    /// <returns>リマインダー。時刻の文字・曜日の値が正しくないときは null。</returns>
    private static Reminder? ToReminder(ReminderSaveRequest request, int no)
    {
        if (!TimeOnly.TryParseExact(request.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            || request.Weekdays is < 0 or > 127)
        {
            return null;
        }

        var byDate = request.Date is not null;
        return new Reminder
        {
            No = no,
            Date = request.Date is { } date ? ReminderDates.ToDateValue(date) : ReminderDates.NoDate,
            Time = ReminderDates.ToTimeValue(time),
            Weekdays = byDate ? Weekdays.None : (Weekdays)request.Weekdays,
            Title = request.Title.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note,
            Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
            IsSpeak = request.IsSpeak,
            TargetUserId = request.TargetUserId,
        };
    }

    /// <summary>
    /// 保存する送信先の番号を決める。宛先が自分でも全員でもないときは、空にする。
    /// </summary>
    /// <param name="request">保存する内容。</param>
    /// <param name="selfId">今のユーザーの番号。</param>
    /// <returns>選んだ送信先。宛先をほかの人にしたリマインダーは、自分の送信先へ送らないため、空。空にすると、自分の送信先の設定がすべて外れる。</returns>
    private static IReadOnlyList<int> EffectiveChannelIds(ReminderSaveRequest request, int selfId)
        => request.TargetUserId == 0 || request.TargetUserId == selfId ? request.ChannelIds : [];

    /// <summary>時刻の整数を、<c>HH:mm</c> の文字にする。</summary>
    /// <param name="value">HHmm の整数。</param>
    /// <returns><c>HH:mm</c> の文字。</returns>
    private static string TimeText(int value) => ReminderDates.ToTime(value)?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";

    /// <summary>一覧の「いつ」の文字。日付指定は日付、曜日指定は曜日。</summary>
    /// <param name="reminder">リマインダー。</param>
    /// <returns><c>yyyy/MM/dd</c> か、「月火水」のような曜日。7 つ全部と 1 つも無いときは「毎日」。</returns>
    private static string WhenText(Reminder reminder)
        => ReminderDates.ToDate(reminder.Date)?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? WeekdaySet.Describe(reminder.Weekdays);

    /// <summary>
    /// 件名から開くリンクにしてよいか確かめる。
    /// </summary>
    /// <param name="link">リマインダーのリンク。</param>
    /// <returns><c>http</c> か <c>https</c> の絶対アドレスのときだけ、そのアドレス。それ以外は null。パソコンのファイルの場所は、iPhone で開けないため。</returns>
    private static string? SafeLink(string? link)
        => link is not null
            && Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
}
