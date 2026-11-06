using MmmSdk.Core.Components.Scheduling;
using MmmSdk.Core.Components.Storage;

namespace MmmTool.Notes.Core;

/// <summary>
/// 今日が実施日のノートを、日付が変わるたびに 1 回探して、知らせる。
/// </summary>
/// <param name="notes">ノートの読み書き</param>
/// <param name="ticker">毎分の合図</param>
/// <remarks>
/// 開始したときと、その後は毎分 1 回、端末ごとにずらした秒に、日付が前回知らせた日と違うかを見る。違うときだけ DB を読む。
/// 00 秒にそろえない。日付が変わった直後に、全員の端末が同じ 1 秒に DB を読みに来るのを避けるため。知らせるのが最大 1 分遅れてもよい。
/// DB モードでログインの前や、接続できないときは、読めない。その日は知らせたことにせず、次の合図でやり直す。
/// 同じ日に何度か起動すると、そのたびに知らせる。人ごとの既読の記録は持たない。
/// 知らせる処理 (<c>notify</c>)は、スレッドプールのスレッドから呼ぶ。画面を触るときは、UI スレッドに切り替える。
/// </remarks>
public sealed class NoteEventMonitor(NoteService notes, IMinuteTicker ticker) : IDisposable
{
    /// <summary>毎分の合図の登録。始める前は null</summary>
    private IMinuteTickSubscription? _subscription;

    /// <summary>始めたか</summary>
    private bool _started;

    /// <summary>今日が実施日のノートを知らせた日。まだ知らせていなければ null</summary>
    private DateOnly? _notifiedDay;

    /// <summary>監視を始める</summary>
    /// <param name="isEnabled">ノートの機能がオンか。オフの間は、読まず、知らせない</param>
    /// <param name="notify">今日が実施日のノートを知らせる処理</param>
    /// <remarks>
    /// 機能をオンにするたびに、起動時の準備が呼ばれる。すでに始めているときは、何もしない。
    /// オフの間も登録は残り、合図のたびに、オンかどうかを見る。オフなら、その回を飛ばす。
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

        // 予想外の失敗 (壊れた行を読んだときなど)は、監視が黙って止まらないよう、合図の仕組みがアプリの受け皿に渡す
        _subscription = ticker.Subscribe(
            _ => CheckAsync(notify),
            new MinuteTickOptions { Timing = MinuteTickTiming.Staggered, RunImmediately = true, IsActive = isEnabled });
    }

    /// <summary>監視を止める</summary>
    public void Dispose() => _subscription?.Dispose();

    /// <summary>日付が変わったかを見て、変わっていたら知らせる</summary>
    /// <param name="notify">知らせる処理</param>
    /// <returns>確認の完了を表すタスク</returns>
    private async Task CheckAsync(Action<IReadOnlyList<Note>> notify)
    {
        var today = notes.Today;
        if (_notifiedDay == today || await TryLoadAsync().ConfigureAwait(false) is not { } found)
        {
            return;
        }

        _notifiedDay = today;
        if (found.Count > 0)
        {
            notify(found);
        }
    }

    /// <summary>今日が実施日のノートを読む</summary>
    /// <returns>ノート。読めなかったとき (ログインの前・接続できない)は null</returns>
    private async Task<IReadOnlyList<Note>?> TryLoadAsync()
    {
        try
        {
            return await notes.LoadEventsTodayAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (DataFileException)
        {
            // 次の合図でやり直す。画面を開いたときには、ノートのページが、読めなかった理由を出す
            return null;
        }
    }
}
