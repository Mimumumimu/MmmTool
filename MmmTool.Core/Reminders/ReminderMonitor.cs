using MmmSdk.Core.Notifications;
using MmmSdk.Core.Settings;
using MmmSdk.Core.Storage;

namespace MmmTool.Core.Reminders;

/// <summary>
/// リマインダーの時刻監視。発動時刻を過ぎた未対応・スヌーズのリマインダーを通知する。アプリ全体で 1 つ。
/// </summary>
/// <param name="reminders">リマインダーの読み書き</param>
/// <param name="settings">汎用設定ストア</param>
/// <param name="timeProvider">現在時刻・タイマーの提供元</param>
/// <remarks>
/// アプリ起動時に <see cref="Start"/> し、ウィンドウの表示有無に関わらず動き続ける。開始直後に 1 回、以後はシステム時刻の毎分 00 秒に判定する
/// （固定間隔ではなく、次の 00 秒までの残り時間をその都度計算する単発タイマーの掛け直し）。
/// 通知の表示は <see cref="Start"/> で受け取ったコールバックに任せる（通知 UI に依存しないため）。
/// </remarks>
public sealed class ReminderMonitor(ReminderService reminders, ISettingsStore settings, TimeProvider timeProvider) : IDisposable
{
    /// <summary>スヌーズの再通知間隔（分）の設定キー</summary>
    public const string SnoozeIntervalKey = "Reminder.SnoozeIntervalMinutes";

    /// <summary>スヌーズの再通知間隔（分）の既定値</summary>
    public const int DefaultSnoozeInterval = 15;

    /// <summary>スヌーズの再通知間隔（分）の最小値</summary>
    public const int MinSnoozeInterval = 5;

    /// <summary>スヌーズの再通知間隔（分）の最大値</summary>
    public const int MaxSnoozeInterval = 999;

    /// <summary>通知のタイトル</summary>
    private const string NotificationTitle = "リマインダー";

    /// <summary>状態を守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>判定のタイマー。開始前・破棄後は null</summary>
    private ITimer? _timer;

    /// <summary>通知を表示するコールバック</summary>
    private Action<string, IReadOnlyList<NotificationItem>>? _notify;

    /// <summary>最後に判定した分（秒以下を切り捨てた時刻）</summary>
    /// <remarks>タイマーが 00 秒より少し早く来たときに、同じ分を 2 回判定しないために使う。</remarks>
    private DateTime? _lastCheckedMinute;

    /// <summary>前回スヌーズ分を通知した分（秒以下を切り捨てた時刻）。まだ無ければ null</summary>
    /// <remarks>スヌーズの再通知間隔は、リマインダーごとではなくモニター全体でこの時刻から数える。メモリ上だけに持つ。</remarks>
    private DateTime? _lastSnoozeNotifiedMinute;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <summary>監視を始める</summary>
    /// <param name="notify">通知を表示するコールバック（タイトル・項目）。タイマーのスレッドから呼ぶので、UI スレッドへの切り替えは渡す側で行う</param>
    /// <exception cref="InvalidOperationException">すでに開始している。</exception>
    public void Start(Action<string, IReadOnlyList<NotificationItem>> notify)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_timer is not null)
            {
                throw new InvalidOperationException("リマインダーの監視はすでに開始しています。");
            }

            _notify = notify;
            // 1 回目の判定が掛け直しで _timer を使うので、代入してから動かす
            _timer = timeProvider.CreateTimer(_ => OnTick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>タイマーが来たら判定して、次の 00 秒に掛け直す</summary>
    /// <remarks>判定中の想定外の例外は握りつぶさない（async void なのでアプリの未処理例外になる）。</remarks>
    private async void OnTick()
    {
        try
        {
            await CheckAsync();
        }
        finally
        {
            ScheduleNext();
        }
    }

    /// <summary>次の 00 秒にタイマーを掛け直す</summary>
    private void ScheduleNext()
    {
        lock (_gate)
        {
            if (_disposed || _timer is null)
            {
                return;
            }

            var now = timeProvider.GetLocalNow().DateTime;
            var next = TruncateToMinute(now).AddMinutes(1);
            _timer.Change(next - now, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>今の分の発動対象を判定して、あれば通知する</summary>
    /// <returns>判定の完了を表すタスク</returns>
    private async Task CheckAsync()
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var minute = TruncateToMinute(now);
        lock (_gate)
        {
            // 00 秒より少し早く来たときは、まだ前の分なので判定しない（次の掛け直しで 00 秒過ぎに来る）
            if (_lastCheckedMinute == minute)
            {
                return;
            }
            _lastCheckedMinute = minute;
        }

        var targets = await GetTriggeredAsync(now);
        if (targets.Count == 0)
        {
            return;
        }

        var states = await GetTodayStatusesAsync(now);

        Action<string, IReadOnlyList<NotificationItem>>? notify;
        List<NotificationItem> items = [];
        lock (_gate)
        {
            var interval = Math.Clamp(settings.Get(SnoozeIntervalKey, DefaultSnoozeInterval), MinSnoozeInterval, MaxSnoozeInterval);
            var snoozeDue = _lastSnoozeNotifiedMinute is not { } last || (minute - last).TotalMinutes >= interval;
            var includesSnooze = false;

            foreach (var reminder in targets)
            {
                switch (states.GetValueOrDefault(reminder.No))
                {
                    case ReminderStatus.None:
                        items.Add(ToItem(reminder));
                        break;
                    case ReminderStatus.Snooze when snoozeDue:
                        items.Add(ToItem(reminder));
                        includesSnooze = true;
                        break;
                }
            }

            if (includesSnooze)
            {
                _lastSnoozeNotifiedMinute = minute;
            }
            notify = _disposed ? null : _notify;
        }

        if (items.Count > 0)
        {
            notify?.Invoke(NotificationTitle, items);
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
        var states = await GetTodayStatusesAsync(now);
        foreach (var reminder in await GetTriggeredAsync(now))
        {
            if (states.GetValueOrDefault(reminder.No) == ReminderStatus.None)
            {
                await reminders.SetStateAsync(reminder.No, today, ReminderStatus.Snooze);
            }
        }

        lock (_gate)
        {
            _lastSnoozeNotifiedMinute = TruncateToMinute(now);
        }
    }

    /// <summary>今日の発動対象で、発動時刻を過ぎたもの（時刻 → 参照番号の順）</summary>
    /// <param name="now">現在の日時</param>
    /// <returns>発動済みのリマインダー</returns>
    private async Task<List<Reminder>> GetTriggeredAsync(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var nowTime = ReminderDates.ToTimeValue(now);
        return [.. (await reminders.GetRemindersAsync())
            .Where(reminder => ReminderDates.OccursOn(reminder, today) && reminder.Time <= nowTime)
            .OrderBy(reminder => reminder.Time)
            .ThenBy(reminder => reminder.No)];
    }

    /// <summary>今日の対応状態（参照番号 → 状態）</summary>
    /// <param name="now">現在の日時</param>
    /// <returns>参照番号をキーにした今日の対応状態。別の日の状態は含めない（未対応として扱うため）</returns>
    private async Task<Dictionary<int, ReminderStatus>> GetTodayStatusesAsync(DateTime now)
    {
        var today = ReminderDates.ToDateValue(now);
        return (await reminders.GetStatesAsync())
            .Where(state => state.Date == today)
            .GroupBy(state => state.BaseNo)
            .ToDictionary(group => group.Key, group => group.Last().Status);
    }

    /// <summary>リマインダーを通知の項目にする（件名をテキスト、リンクがあればリンク先に）</summary>
    /// <param name="reminder">リマインダー</param>
    /// <returns>通知の項目</returns>
    private static NotificationItem ToItem(Reminder reminder)
        => new(reminder.Title, string.IsNullOrWhiteSpace(reminder.Link) ? null : reminder.Link);

    /// <summary>秒以下を切り捨てる</summary>
    /// <param name="value">日時</param>
    /// <returns>秒以下を 0 にした日時</returns>
    private static DateTime TruncateToMinute(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    /// <summary>監視を止める</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            _notify = null;
        }
    }
}
