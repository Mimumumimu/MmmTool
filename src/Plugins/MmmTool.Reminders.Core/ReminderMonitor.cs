using MmmSdk.Core.Components.Notifications;
using MmmSdk.Core.Components.Scheduling;
using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

/// <summary>
/// リマインダーの時刻監視。発動時刻を過ぎた未対応・スヌーズのリマインダーを通知する。アプリ全体で 1 つ。
/// </summary>
/// <param name="reminders">リマインダーの読み書き</param>
/// <param name="settings">リマインダー用の設定 (スヌーズの再通知間隔)</param>
/// <param name="ticker">毎分の合図</param>
/// <param name="timeProvider">現在時刻・タイマーの提供元</param>
/// <remarks>
/// アプリ起動時に <see cref="Start"/> し、ウィンドウの表示有無に関わらず動き続ける。開始直後に 1 回、以後はシステム時刻の毎分 00 秒に判定する。
/// 毎分の合図は、アプリ全体で 1 つの <see cref="ITicker"/> から受け取る。通知する項目の判定は <see cref="ReminderEvaluator"/> が行う。ここはそれらをつなぎ、スヌーズの通知時刻を覚える。
/// 通知の表示は <see cref="Start"/> で受け取ったコールバックに任せる。通知 UI に依存しないため。
/// </remarks>
public sealed class ReminderMonitor(ReminderService reminders, ReminderSettingsService settings, ITicker ticker, TimeProvider timeProvider) : IDisposable
{
    /// <summary>通知のタイトル</summary>
    private const string NotificationTitle = "リマインダー";

    /// <summary>状態を守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>毎分の合図の登録。開始前・破棄後は null</summary>
    private ITickSubscription? _subscription;

    /// <summary>通知を表示するコールバック</summary>
    private Action<string, IReadOnlyList<NotificationItem>, string?>? _notify;

    /// <summary>前回スヌーズ分を通知した分で、秒以下を切り捨てた時刻。まだ無ければ null</summary>
    /// <remarks>スヌーズの再通知間隔は、リマインダーごとではなくモニター全体でこの時刻から数える。メモリ上だけに持つ。</remarks>
    private DateTime? _lastSnoozeNotifiedMinute;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <summary>監視を始める</summary>
    /// <param name="notify">通知を表示するコールバックで、引数はタイトル・項目・読み上げる文。読み上げ対象が無ければ文は null。タイマーのスレッドから呼ぶので、UI スレッドへの切り替えは渡す側で行う</param>
    /// <param name="isActive">今、判定してよいか。合図のたびに確かめ、false なら今回を飛ばす。管理者に機能を無効にされた人や、機能をオフにした人では、判定しない。null なら常に判定する</param>
    /// <exception cref="InvalidOperationException">すでに開始している。</exception>
    public void Start(Action<string, IReadOnlyList<NotificationItem>, string?> notify, Func<bool>? isActive = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_notify is not null)
            {
                throw new InvalidOperationException("リマインダーの監視はすでに開始しています。");
            }
            _notify = notify;
        }

        var subscription = ticker.Subscribe(CheckAsync, new TickOptions { RunImmediately = true, IsActive = isActive });
        lock (_gate)
        {
            _subscription = subscription;
        }
    }

    /// <summary>発動済みで未対応のリマインダーを、今日のスヌーズにする</summary>
    /// <returns>スヌーズへの切り替えの完了を表すタスク</returns>
    /// <remarks>
    /// 通知をクリックして「気づいた」とみなすときに使う。次の再通知はスヌーズ間隔の後にするため、スヌーズの間隔もここから数え直す。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task SnoozeTriggeredAsync()
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var today = ReminderDates.ToDateValue(now);
        var changes = (await GetTriggeredAsync(now))
            .Where(target => target.Status == ReminderStatus.None)
            .Select(target => new ReminderStateChange(target.Reminder.No, today, ReminderStatus.Snooze))
            .ToList();
        await reminders.SetStatesAsync(changes);

        lock (_gate)
        {
            _lastSnoozeNotifiedMinute = MinuteTime.TruncateToMinute(now);
        }
    }

    /// <summary>監視を止める</summary>
    public void Dispose()
    {
        ITickSubscription? subscription;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _notify = null;
            subscription = _subscription;
            _subscription = null;
        }
        subscription?.Dispose();
    }

    /// <summary>今の分の発動対象を判定して、あれば通知する</summary>
    /// <param name="now">現在の日時</param>
    /// <returns>判定の完了を表すタスク</returns>
    private async Task CheckAsync(DateTime now)
    {
        var targets = await GetTriggeredAsync(now);
        if (targets.Count == 0)
        {
            return;
        }

        var minute = MinuteTime.TruncateToMinute(now);
        Action<string, IReadOnlyList<NotificationItem>, string?>? notify;
        ReminderEvaluation evaluation;
        lock (_gate)
        {
            evaluation = ReminderEvaluator.Evaluate(targets, minute, _lastSnoozeNotifiedMinute, settings.SnoozeIntervalMinutes);
            if (evaluation.IncludesSnooze)
            {
                _lastSnoozeNotifiedMinute = minute;
            }
            notify = _disposed ? null : _notify;
        }

        if (evaluation.Items.Count > 0)
        {
            notify?.Invoke(NotificationTitle, evaluation.Items, evaluation.SpeechText);
        }
    }

    /// <summary>今日の発動対象で、発動時刻を過ぎたもの (時刻 → 参照番号の順)と、その対応状態</summary>
    /// <param name="now">現在の日時</param>
    /// <returns>発動済みのリマインダーと今日の対応状態 (今日の対象の組み立ては <see cref="ReminderService.GetTargetsAsync"/>)</returns>
    private async Task<List<ReminderTarget>> GetTriggeredAsync(DateTime now)
    {
        var nowTime = ReminderDates.ToTimeValue(now);
        return [.. (await reminders.GetTargetsAsync(now)).Where(target => target.Reminder.Time <= nowTime)];
    }
}
