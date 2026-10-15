using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 登録された仕事 (<see cref="IBatchJob"/>)を、仕事ごとの間隔で動かす。
/// </summary>
/// <param name="jobs">登録された仕事 (登録順に動かす)</param>
/// <param name="log">送信のログ (古い日のファイルの整理に使う)</param>
/// <param name="alerts">気づいてほしい失敗の窓口</param>
/// <param name="time">現在時刻・タイマーの提供元</param>
/// <remarks>
/// <para>
/// 1 秒ごとに時計を確かめ、間隔が来た仕事を、登録順に 1 つずつ動かす。仕事の間隔 (<see cref="IBatchJob.Interval"/>)は、0 時からの区切りに合わせる
/// (毎分の仕事は毎分 00 秒、5 秒の仕事は 5 秒ごとの区切り)。開始直後に、すべての仕事を 1 回動かす。
/// 前の実行が終わっていないときは、その回を飛ばす (間隔が来たままなので、終わりしだい動く)。
/// </para>
/// <para>
/// 画面の「再送」も、同じゲート (<see cref="RunExclusiveAsync"/>)を通る (仕事と同時に送らない)。
/// 破棄 (Host の破棄)で止まり、実行中の仕事も取り消す。このクラスは、個々の機能の名前を持たない。
/// </para>
/// </remarks>
public sealed class BatchMonitor(IEnumerable<IBatchJob> jobs, ISendLog log, BatchAlertService alerts, TimeProvider time) : IDisposable
{
    /// <summary>時計を確かめる間隔</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    /// <summary>登録された仕事 (登録順)</summary>
    private readonly IReadOnlyList<IBatchJob> _jobs = [.. jobs];

    /// <summary>仕事ごとの、次に動かす時刻 (ローカル)。まだ動かしていない仕事は、すぐ動かす</summary>
    private readonly Dictionary<IBatchJob, DateTime> _nextRun = [];

    /// <summary>同時に 1 つだけ実行するためのゲート</summary>
    /// <remarks>破棄しない (実行中の処理が、終わるときに解放するため。プロセスの終了で消える)。</remarks>
    private readonly SemaphoreSlim _running = new(1, 1);

    /// <summary>破棄したときに、実行中の仕事を取り消すためのトークン</summary>
    /// <remarks>破棄しない (実行中の処理が、トークンを読むため。プロセスの終了で消える)。</remarks>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>最初の実行が終わったことを知らせる</summary>
    private readonly TaskCompletionSource _firstRun = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>時計を確かめるタイマー。開始前・破棄後は null</summary>
    private ITimer? _timer;

    /// <summary>古いログのファイルを整理した日</summary>
    private DateOnly? _archivedOn;

    /// <summary>最初の実行が終わるまでの待機</summary>
    /// <remarks>ホストが、最初の実行のあとに <see cref="HasError"/> を見て、画面を出すかを決めるために使う。</remarks>
    public Task FirstRunCompleted => _firstRun.Task;

    /// <summary>どれかの仕事が、最後の実行で失敗したか (DB の読み書きができなかった)</summary>
    public bool HasError => _jobs.Any(job => job.LastError is not null);

    /// <summary>監視を始める</summary>
    /// <exception cref="InvalidOperationException">すでに始めている。</exception>
    public void Start()
    {
        if (_timer is not null)
        {
            throw new InvalidOperationException("すでに開始しています。");
        }

        _timer = time.CreateTimer(_ => OnTick(), null, TimeSpan.Zero, TickInterval);
    }

    /// <summary>止める</summary>
    /// <remarks>実行中の仕事を取り消し、次の実行をやめる。</remarks>
    public void Dispose()
    {
        _stop.Cancel();
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>間隔を待たず、すべての仕事を今すぐ 1 回実行する</summary>
    /// <returns>実行の完了を表すタスク。仕事が動いている最中は、その完了まで待たずに、何もしない</returns>
    /// <remarks>接続の設定を保存した直後に、画面の失敗の表示を、最新にするために使う (次の実行まで、前の失敗が残らないように)。</remarks>
    public Task RunNowAsync() => RunAsync(force: true);

    /// <summary>仕事と同時にならないように、処理を 1 つ実行する (画面の「再送」「今すぐ送る」)</summary>
    /// <param name="action">実行する処理。破棄で取り消されるトークンを受け取る</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <remarks>仕事が動いている最中は、その終わりを待ってから実行する (同時に送らないため)。</remarks>
    /// <exception cref="DataFileException">処理が、設定が足りない・接続できない・表が無い・保存できなかったことを伝えた (メッセージは画面に出せる)。</exception>
    public async Task RunExclusiveAsync(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _running.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            await action(_stop.Token).ConfigureAwait(false);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>1 秒ごとに、間隔が来た仕事を動かす</summary>
    /// <remarks>処理中の想定外の例外は握りつぶさない (async void なのでアプリの未処理例外になる)。</remarks>
    private async void OnTick() => await RunAsync(force: false).ConfigureAwait(false);

    /// <summary>間隔が来た仕事 (<paramref name="force"/> なら、すべて)を、登録順に動かす</summary>
    /// <param name="force">間隔にかかわらず、すべて動かすか</param>
    /// <returns>実行の完了を表すタスク</returns>
    private async Task RunAsync(bool force)
    {
        if (!await _running.WaitAsync(0).ConfigureAwait(false))
        {
            // 最初の実行の前に、画面の再送がゲートを持っていたときも、ホストの起動を待たせない (仕事は、あとで動く)
            _firstRun.TrySetResult();
            return;
        }

        try
        {
            var now = time.GetLocalNow().DateTime;
            ArchiveOldLogs(DateOnly.FromDateTime(now));
            foreach (var job in _jobs)
            {
                if (!force && _nextRun.TryGetValue(job, out var due) && now < due)
                {
                    continue;
                }

                _nextRun[job] = NextBoundary(now, job.Interval);
                await job.RunAsync(now, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // 終了で止まった
        }
        finally
        {
            _running.Release();
            _firstRun.TrySetResult();
        }
    }

    /// <summary>次の区切りの時刻を求める</summary>
    /// <param name="now">今の日時 (ローカル)</param>
    /// <param name="interval">仕事の間隔</param>
    /// <returns>今より後の、間隔の区切り (間隔の倍数の時刻。毎分の仕事なら次の 00 秒)</returns>
    private static DateTime NextBoundary(DateTime now, TimeSpan interval)
        => new((now.Ticks / interval.Ticks + 1) * interval.Ticks, now.Kind);

    /// <summary>日付が変わった最初の実行で、古い日のログのファイルを <c>old</c> フォルダーへ移す</summary>
    /// <param name="today">今日</param>
    /// <remarks>ログは機能に共通なので、特定の仕事ではなく、ここで行う。移せなかったときは、通知で知らせる (送信そのものは止めない)。</remarks>
    private void ArchiveOldLogs(DateOnly today)
    {
        if (_archivedOn == today)
        {
            return;
        }

        _archivedOn = today;
        try
        {
            log.MoveOldFiles(today);
        }
        catch (DataFileException ex)
        {
            alerts.Raise(ex.Message);
        }
    }
}
