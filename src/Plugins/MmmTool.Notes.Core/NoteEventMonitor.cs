using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;

namespace MmmTool.Notes.Core;

/// <summary>
/// 今日が実施日のノートを、日付が変わるたびに 1 回探して、知らせる。
/// </summary>
/// <param name="notes">ノートの読み書き</param>
/// <param name="time">現在時刻の取得元</param>
/// <remarks>
/// 開始したときと、その後は <see cref="Interval"/> ごとに、日付が前回知らせた日と違うかを見る。違うときだけ DB を読む。
/// DB モードでログインの前や、接続できないときは、読めない。その日は知らせたことにせず、次の周期でやり直す。
/// 同じ日に何度か起動すると、そのたびに知らせる。人ごとの既読の記録は持たない。
/// 知らせる処理 (<c>notify</c>)は、スレッドプールのスレッドから呼ぶ。画面を触るときは、UI スレッドに切り替える。
/// </remarks>
public sealed class NoteEventMonitor(NoteService notes, TimeProvider time) : IDisposable
{
    /// <summary>日付が変わったかを見る間隔</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>監視を止めるためのトークンの元</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>始めたか</summary>
    private bool _started;

    /// <summary>監視を始める</summary>
    /// <param name="isEnabled">ノートの機能がオンか。オフの間は、読まず、知らせない</param>
    /// <param name="notify">今日が実施日のノートを知らせる処理</param>
    /// <remarks>
    /// 機能をオンにするたびに、起動時の準備が呼ばれる。すでに始めているときは、何もしない。
    /// オフの間も監視は続き、周期ごとに、オンかどうかを見る。
    /// </remarks>
    public void Start(Func<bool> isEnabled, Action<IReadOnlyList<Note>> notify)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentNullException.ThrowIfNull(notify);
        if (_started)
        {
            return;
        }
        _started = true;

        // 予想外の失敗 (壊れた行を読んだときなど)は、監視が黙って止まらないよう、アプリの受け皿に渡す
        Task.Run(() => RunAsync(isEnabled, notify, _stop.Token)).Forget();
    }

    /// <summary>監視を止める</summary>
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>日付が変わったかを、周期ごとに見て、変わっていたら知らせる</summary>
    /// <param name="isEnabled">ノートの機能がオンか</param>
    /// <param name="notify">知らせる処理</param>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>監視を止めるまでの完了を表すタスク</returns>
    private async Task RunAsync(Func<bool> isEnabled, Action<IReadOnlyList<Note>> notify, CancellationToken cancellationToken)
    {
        DateOnly? notifiedDay = null;
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                var today = notes.Today;
                if (notifiedDay != today && isEnabled() && await TryLoadAsync(cancellationToken).ConfigureAwait(false) is { } found)
                {
                    notifiedDay = today;
                    if (found.Count > 0)
                    {
                        notify(found);
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // 監視を止めた
        }
    }

    /// <summary>今日が実施日のノートを読む</summary>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>ノート。読めなかったとき (ログインの前・接続できない)は null</returns>
    private async Task<IReadOnlyList<Note>?> TryLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await notes.LoadEventsTodayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DataFileException)
        {
            // 次の周期でやり直す。画面を開いたときには、ノートのページが、読めなかった理由を出す
            return null;
        }
    }
}
