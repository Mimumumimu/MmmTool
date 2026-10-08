using MmmSdk.Core.Components.Scheduling;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 毎分、送るものがあるか確かめて、送る (SDK の <see cref="MinuteScheduler"/>で動かす)。
/// </summary>
/// <param name="service">送信の処理</param>
/// <param name="time">現在時刻の提供元</param>
/// <remarks>
/// 開始直後に 1 回、以後は毎分 00 秒に実行する。前の実行が終わっていないときは、その分を飛ばす (次の分に、また試す)。
/// 破棄 (Host の破棄)で止まり、実行中の送信も取り消す。
/// </remarks>
public sealed class SendMonitor(ReminderSendService service, TimeProvider time) : IDisposable
{
    /// <summary>毎分の実行のスケジューラー</summary>
    private readonly MinuteScheduler _scheduler = new(time);

    /// <summary>同時に 1 つだけ実行するためのゲート</summary>
    /// <remarks>破棄しない (実行中の処理が、終わるときに解放するため。プロセスの終了で消える)。</remarks>
    private readonly SemaphoreSlim _running = new(1, 1);

    /// <summary>破棄したときに、実行中の送信を取り消すためのトークン</summary>
    /// <remarks>破棄しない (実行中の処理が、トークンを読むため。プロセスの終了で消える)。</remarks>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>監視を始める</summary>
    /// <exception cref="InvalidOperationException">すでに始めている。</exception>
    public void Start() => _scheduler.Start(OnMinuteAsync);

    /// <summary>止める</summary>
    /// <remarks>実行中の送信を取り消し、次の分の実行をやめる。</remarks>
    public void Dispose()
    {
        _stop.Cancel();
        _scheduler.Dispose();
    }

    /// <summary>次の分を待たず、今すぐ 1 回実行する</summary>
    /// <returns>実行の完了を表すタスク。毎分の実行が動いている最中は、その完了まで待たずに、何もしない</returns>
    /// <remarks>接続の設定を保存した直後に、画面の失敗の表示を、最新にするために使う (次の分まで、前の失敗が残らないように)。</remarks>
    public Task RunNowAsync() => OnMinuteAsync(time.GetLocalNow().DateTime);

    /// <summary>今日の分を、今すぐ 1 回送る (画面の「再送」「今すぐ送る」)</summary>
    /// <param name="target">送る対象</param>
    /// <returns>送信の完了を表すタスク</returns>
    /// <remarks>毎分の実行が動いている最中は、その終わりを待ってから送る (同時に送らないため)。</remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    public async Task ResendAsync(SendTarget target)
    {
        await _running.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            await service.ResendAsync(target, DateOnly.FromDateTime(time.GetLocalNow().DateTime), _stop.Token).ConfigureAwait(false);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>毎分の実行</summary>
    /// <param name="now">今の日時 (ローカル)</param>
    /// <returns>実行の完了を表すタスク</returns>
    private async Task OnMinuteAsync(DateTime now)
    {
        if (!await _running.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await service.RunAsync(now, _stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // 終了で止まった
        }
        finally
        {
            _running.Release();
        }
    }
}
