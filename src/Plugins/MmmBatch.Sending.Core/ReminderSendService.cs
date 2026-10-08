using MmmSdk.Core.Components.Storage;
using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 時刻になったリマインダーを、選んだ送信先へ、一度だけ送る。
/// </summary>
/// <remarks>
/// <para>
/// 1 回の実行 (<see cref="RunAsync"/>)は、送る対象 (今日発動し、時刻が過ぎたもの)ごとに、送信の状況の行を先に作ってから送る。
/// 行を作れた (一意制約に当たらなかった)ものだけが送る。失敗 (<see cref="SendStatus.Failed"/>)は、待つ時間を空けて、回数の上限まで送り直す。
/// 送信中 (<see cref="SendStatus.Pending"/>)のまま長く止まっている行 (送っている途中で落ちた)も、送り直す。
/// 送り直しの前に送信が済んでいたときは、二重に送られうる (取りこぼすより、重複を許す側に倒す)。
/// </para>
/// <para>
/// 宛先・対応状態 (完了・スヌーズ)は見ない。送信は、登録した人が選んだ送信先へ、時刻に一度だけ送るもので、人ごとの対応状態とは別。
/// 起動した直後の実行でも、今日の時刻を過ぎた分を送る (止まっていた間の分を、起動後に送る)。
/// </para>
/// </remarks>
/// <param name="source">送る対象の読み出し口</param>
/// <param name="statuses">送信の状況の保存先</param>
/// <param name="notifiers">外部へ送る口 (送信先の区分ごと)</param>
/// <param name="log">送信のログ (送った 1 回ごとの記録)</param>
/// <param name="time">現在時刻の提供元</param>
public sealed class ReminderSendService(
    ISendTargetSource source, IReminderSendStatusRepository statuses, IEnumerable<INotifier> notifiers, ISendLog log, TimeProvider time)
{
    /// <summary>1 つの送信先へ送ろうとする回数の上限 (最初の 1 回を含む)</summary>
    public const int MaxAttempts = 5;

    /// <summary>送信中のまま、送り直しの対象にするまでの時間</summary>
    private static readonly TimeSpan StalePendingAge = TimeSpan.FromMinutes(10);

    /// <summary>区分ごとの、外部へ送る口</summary>
    private readonly Dictionary<NotificationChannelKind, INotifier> _notifiers = notifiers.ToDictionary(notifier => notifier.Kind);

    /// <summary>送信の状況が変わった (送った・失敗した・送り直しを始めた)</summary>
    /// <remarks>任意のスレッドから発火する。</remarks>
    public event EventHandler? Changed;

    /// <summary>気づいてほしい失敗が起きた (送信の失敗・DB の読み書きの失敗)</summary>
    /// <remarks>
    /// 引数は、画面に出せるメッセージ。送信の失敗は、最初の失敗と、回数の上限での最後の失敗の 2 回だけ知らせる (送り直しのたびに知らせない)。
    /// DB の失敗は、失敗の内容が変わったときだけ知らせる。任意のスレッドから発火する。
    /// </remarks>
    public event EventHandler<string>? AlertRaised;

    /// <summary>毎分の実行が終わった (変わったことが無くても発火する)</summary>
    /// <remarks>今日の送信予定の画面が、毎分、読み直すために使う。任意のスレッドから発火する。</remarks>
    public event EventHandler? Ran;

    /// <summary>古いログのファイルを整理した日</summary>
    private DateOnly? _archivedOn;

    /// <summary>最後の実行の失敗 (DB の読み書きができなかった)。無ければ null</summary>
    /// <remarks>次の実行が成功すると、消える。変わったときも <see cref="Changed"/> が発火する。</remarks>
    public string? LastError { get; private set; }

    /// <summary>今の時刻に送るものを、送る</summary>
    /// <param name="now">今の日時 (ローカル)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    /// <remarks>DB の読み書きの失敗は、<see cref="LastError"/> に残して、その回を終える (次の分に、また試す)。送信の失敗は、行の状態に残す。</remarks>
    public async Task RunAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(now);
        var nowTime = ReminderDates.ToTimeValue(now);
        var changed = false;

        // 日付が変わった最初の実行で、古い日のログのファイルを old フォルダーへ移す (ログは残し、移すだけ)
        if (_archivedOn != today)
        {
            _archivedOn = today;
            try
            {
                log.MoveOldFiles(today);
            }
            catch (DataFileException ex)
            {
                AlertRaised?.Invoke(this, ex.Message);
            }
        }

        try
        {
            var targets = await source.GetTargetsAsync(cancellationToken).ConfigureAwait(false);
            var existing = (await statuses.GetByDateAsync(today, cancellationToken).ConfigureAwait(false))
                .ToDictionary(status => (status.ReminderId, status.ChannelId));

            var due = targets
                .Where(target => IsDue(target, today, nowTime))
                .OrderBy(target => target.Reminder.Time)
                .ThenBy(target => target.Reminder.No);
            foreach (var target in due)
            {
                cancellationToken.ThrowIfCancellationRequested();

                existing.TryGetValue((target.Reminder.No, target.Channel.Id), out var current);
                var claimed = await ClaimAsync(target, current, today, cancellationToken).ConfigureAwait(false);
                if (claimed is null)
                {
                    continue;
                }

                changed = true;
                await SendAsync(target, claimed, cancellationToken).ConfigureAwait(false);
            }

            // 昨日の、送れなかった (または、送信中のまま止まった)ものも、上限の回数まで送り直す (日付をまたいでも、送り直しは数分で終わる)
            changed |= await RetryPreviousDayAsync(targets, today.AddDays(-1), cancellationToken).ConfigureAwait(false);

            changed |= SetLastError(null);
        }
        catch (DataFileException ex)
        {
            var errorChanged = SetLastError(ex.Message);
            changed |= errorChanged;
            if (errorChanged)
            {
                AlertRaised?.Invoke(this, ex.Message);
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        Ran?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>送る対象が、今日、送る時刻を過ぎているか</summary>
    /// <param name="target">送る対象</param>
    /// <param name="today">今日</param>
    /// <param name="nowTime">今の時刻 (HHmm の整数)</param>
    /// <returns>今日発動し、時刻が正しく、過ぎていて、送信設定が今日の発動時刻より前からあれば true</returns>
    /// <remarks>送信設定を、今日の発動時刻より後に付けた (更新した)ときは、今日の分は送らない (付けた時点で過ぎている分を、すぐ送らないため。明日からは送る)。</remarks>
    private static bool IsDue(SendTarget target, DateOnly today, int nowTime)
        => target.Reminder.Time <= nowTime && IsPlanned(target, today);

    /// <summary>送る対象が、今日の送信予定か (時刻が過ぎているかは問わない)</summary>
    /// <param name="target">送る対象</param>
    /// <param name="today">今日</param>
    /// <returns>今日発動し、時刻が正しく、送信設定が今日の発動時刻より前からあれば true</returns>
    private static bool IsPlanned(SendTarget target, DateOnly today)
        => ReminderDates.ToTime(target.Reminder.Time) is { } time
            && ReminderDates.OccursOn(target.Reminder, today)
            && target.SettingUpdatedAt.LocalDateTime <= today.ToDateTime(time);

    /// <summary>今日の送信予定を取得する</summary>
    /// <param name="now">今の日時 (ローカル)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>今日の送信予定 (時刻 → リマインダーの番号の順)。今日の分でないもの・削除したもの・送信先なしは含まない</returns>
    /// <remarks>保存した時点で、送る時刻が過ぎていて、今日は送らないものも含める (<see cref="SendPlanItem.IsSkipped"/>。なぜ送られないか、見えるように)。</remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public async Task<IReadOnlyList<SendPlanItem>> GetTodayPlanAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(now);
        var targets = await source.GetTargetsAsync(cancellationToken).ConfigureAwait(false);
        var existing = (await statuses.GetByDateAsync(today, cancellationToken).ConfigureAwait(false))
            .ToDictionary(status => (status.ReminderId, status.ChannelId));

        return [.. targets
            .Where(target => ReminderDates.ToTime(target.Reminder.Time) is not null && ReminderDates.OccursOn(target.Reminder, today))
            .Select(target =>
            {
                var status = existing.GetValueOrDefault((target.Reminder.No, target.Channel.Id));
                return new SendPlanItem(target, status, status is null && !IsPlanned(target, today));
            })
            .OrderBy(item => item.Target.Reminder.Time)
            .ThenBy(item => item.Target.Reminder.No)];
    }

    /// <summary>今日の分を、状態や時刻にかかわらず、今すぐ 1 回送る (画面の「再送」「今すぐ送る」)</summary>
    /// <param name="target">送る対象</param>
    /// <param name="now">今の日時 (ローカル)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信と記録の完了を表すタスク</returns>
    /// <remarks>
    /// <para>
    /// まだ時刻が来ていない予定 (今日送る予定で、まだ送ろうとしていない)は、送るだけで、「今日の送信の状況」は作らない。
    /// テスト送信が、予定の送信を消さないため (時刻になれば、予定どおり、もう一度送られる)。送ったことは、送信のログに残る。
    /// </para>
    /// <para>
    /// それ以外は、送信済み・回数の上限に達した失敗も送り直せる (回数は数え続ける)。時刻が過ぎているのに、まだ送ろうとしていないものは、行を作って送る。
    /// 同じ送信を、別の MmmBatch・毎分の実行が同時に行っていたときは、先に権利を取ったほうだけが送り、こちらは何もしない。
    /// </para>
    /// </remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった・保存できなかった (メッセージは画面に出せる)。</exception>
    public async Task ResendAsync(SendTarget target, DateTime now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var today = DateOnly.FromDateTime(now);
        var current = (await statuses.GetByDateAsync(today, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(status => status.ReminderId == target.Reminder.No && status.ChannelId == target.Channel.Id);

        // まだ時刻が来ていない予定は、送るだけ (今日の送信の状況は作らない。時刻になれば、予定どおり送られる)
        if (current is null && IsPlanned(target, today) && target.Reminder.Time > ReminderDates.ToTimeValue(now))
        {
            await SendWithoutStatusAsync(target, cancellationToken).ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        ReminderSendStatus? claimed;
        if (current is null)
        {
            claimed = await statuses.TryCreateAsync(target.Reminder.No, target.Channel.Id, today, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            claimed = await statuses.TryClaimRetryAsync(current, cancellationToken).ConfigureAwait(false)
                ? current with { Status = SendStatus.Pending, Attempts = current.Attempts + 1 }
                : null;
        }

        if (claimed is null)
        {
            return;
        }

        await SendAsync(target, claimed, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>送信の状況を作らず・変えずに、1 回送って、結果を送信のログだけに残す (まだ時刻が来ていない予定へのテスト送信)</summary>
    /// <param name="target">送る対象</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信と記録の完了を表すタスク</returns>
    /// <remarks>回数は 0 で記録する (予定の送信の回数には数えない)。失敗は、ログに理由が残る (トレイの通知は出さない。操作した人が、画面で見ているため)。</remarks>
    private async Task SendWithoutStatusAsync(SendTarget target, CancellationToken cancellationToken)
    {
        var status = SendStatus.Sent;
        var error = "";
        if (!_notifiers.TryGetValue(target.Channel.Kind, out var notifier))
        {
            status = SendStatus.Failed;
            error = "この種類の送信先には、まだ送れません。";
        }
        else
        {
            try
            {
                await notifier.SendAsync(target.Channel, SendMessage.FromReminder(target.Reminder), cancellationToken).ConfigureAwait(false);
            }
            catch (SendFailedException ex)
            {
                status = SendStatus.Failed;
                error = ex.Message;
            }
        }

        try
        {
            await log.AppendAsync(
                new SendLogEntry(
                    time.GetLocalNow(), target.Reminder.No, target.Reminder.Title, target.OwnerName, target.Channel.Id, target.Channel.Kind, target.Channel.Name,
                    0, status, error),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DataFileException ex)
        {
            AlertRaised?.Invoke(this, ex.Message);
        }
    }

    /// <summary>送る権利を取る (行を作る・送り直しの権利を取る)</summary>
    /// <param name="target">送る対象</param>
    /// <param name="current">今日の送信の状況。まだ無ければ null</param>
    /// <param name="today">今日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送る権利を取れたときの、送信中の行。送らないとき (済み・上限・待ち時間・別の MmmBatch が先)は null</returns>
    private async Task<ReminderSendStatus?> ClaimAsync(SendTarget target, ReminderSendStatus? current, DateOnly today, CancellationToken cancellationToken)
    {
        if (current is null)
        {
            return await statuses.TryCreateAsync(target.Reminder.No, target.Channel.Id, today, cancellationToken).ConfigureAwait(false);
        }

        return await ClaimRetryAsync(current, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>送り直しの権利を取る (済み・上限・待ち時間のものは取らない)</summary>
    /// <param name="current">今の送信の状況</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>権利を取れたときの、送信中の行。送らないとき (済み・上限・待ち時間・別の MmmBatch が先)は null</returns>
    private async Task<ReminderSendStatus?> ClaimRetryAsync(ReminderSendStatus current, CancellationToken cancellationToken)
    {
        if (current.Status == SendStatus.Sent || current.Attempts >= MaxAttempts)
        {
            return null;
        }

        // 失敗は、回数 × 1 分、送信中のまま止まったものは 10 分、待ってから送り直す
        var waited = time.GetUtcNow() - current.UpdatedAt;
        var wait = current.Status == SendStatus.Failed ? TimeSpan.FromMinutes(current.Attempts) : StalePendingAge;
        if (waited < wait)
        {
            return null;
        }

        return await statuses.TryClaimRetryAsync(current, cancellationToken).ConfigureAwait(false)
            ? current with { Status = SendStatus.Pending, Attempts = current.Attempts + 1 }
            : null;
    }

    /// <summary>昨日の、送れなかったもの・送信中のまま止まったものを、送り直す</summary>
    /// <param name="targets">送る対象 (送信設定が今もあるものだけ。削除されたリマインダー・送信先は送り直さない)</param>
    /// <param name="yesterday">昨日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送り直しを始めたものがあれば true</returns>
    /// <remarks>新しい行は作らない (昨日の分を、今から新しく送らない)。送る内容は、今のリマインダーの内容。</remarks>
    private async Task<bool> RetryPreviousDayAsync(IReadOnlyList<SendTarget> targets, DateOnly yesterday, CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var status in await statuses.GetByDateAsync(yesterday, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = targets.FirstOrDefault(item => item.Reminder.No == status.ReminderId && item.Channel.Id == status.ChannelId);
            if (target is null || await ClaimRetryAsync(status, cancellationToken).ConfigureAwait(false) is not { } claimed)
            {
                continue;
            }

            changed = true;
            await SendAsync(target, claimed, cancellationToken).ConfigureAwait(false);
        }
        return changed;
    }

    /// <summary>送って、結果を行に残す</summary>
    /// <param name="target">送る対象</param>
    /// <param name="claimed">送る権利を取った、送信中の行</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信と記録の完了を表すタスク</returns>
    private async Task SendAsync(SendTarget target, ReminderSendStatus claimed, CancellationToken cancellationToken)
    {
        if (!_notifiers.TryGetValue(target.Channel.Kind, out var notifier))
        {
            await FinishAsync(target, claimed, SendStatus.Failed, "この種類の送信先には、まだ送れません。", cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await notifier.SendAsync(target.Channel, SendMessage.FromReminder(target.Reminder), cancellationToken).ConfigureAwait(false);
        }
        catch (SendFailedException ex)
        {
            await FinishAsync(target, claimed, SendStatus.Failed, ex.Message, cancellationToken).ConfigureAwait(false);
            if (claimed.Attempts == 1 || claimed.Attempts >= MaxAttempts)
            {
                AlertRaised?.Invoke(this, $"「{target.Reminder.Title}」を「{target.Channel.Name}」へ送れませんでした。({ex.Message})");
            }
            return;
        }

        await FinishAsync(target, claimed, SendStatus.Sent, "", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>結果を、送信の状況 (今の状態)に残し、送信のログに 1 行足す</summary>
    /// <param name="target">送った対象</param>
    /// <param name="claimed">送る権利を取った、送信中の行</param>
    /// <param name="status">結果 (<see cref="SendStatus.Sent"/> か <see cref="SendStatus.Failed"/>)</param>
    /// <param name="error">失敗の理由。成功は空文字</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>記録の完了を表すタスク</returns>
    /// <remarks>ログを書けなくても、送信そのものは失敗にしない (気づけるよう、通知は出す)。</remarks>
    private async Task FinishAsync(SendTarget target, ReminderSendStatus claimed, SendStatus status, string error, CancellationToken cancellationToken)
    {
        if (status == SendStatus.Sent)
        {
            await statuses.MarkSentAsync(claimed.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await statuses.MarkFailedAsync(claimed.Id, error, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await log.AppendAsync(
                new SendLogEntry(
                    time.GetLocalNow(), target.Reminder.No, target.Reminder.Title, target.OwnerName, target.Channel.Id, target.Channel.Kind, target.Channel.Name,
                    claimed.Attempts, status, error),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DataFileException ex)
        {
            AlertRaised?.Invoke(this, ex.Message);
        }
    }

    /// <summary>最後の失敗を記録する</summary>
    /// <param name="message">失敗のメッセージ。無ければ null</param>
    /// <returns>前回と違えば true</returns>
    private bool SetLastError(string? message)
    {
        var changed = LastError != message;
        LastError = message;
        return changed;
    }
}
