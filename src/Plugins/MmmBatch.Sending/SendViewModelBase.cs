using MmmBatch.Sending.Core;
using MmmSdk.Core.Utilities;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch.Sending;

/// <summary>
/// 送信の一覧の画面 (今日の送信予定・最近の依頼・履歴)の ViewModel の共通部分。
/// </summary>
/// <remarks>
/// 機能の仕事の結果が変わったら、<see cref="IBatchJob.Changed"/> を受けて、UI スレッドで読み直す。
/// 読み込みの失敗の表示と、「接続の設定」から読み直す処理、古い読み込みの結果を捨てる世代の管理は、共通の <see cref="DatabaseViewModelBase"/> が持つ。
/// 変更の通知を、作ったスレッドへ戻して反映するため、UI スレッドで作ること。
/// </remarks>
public abstract class SendViewModelBase : DatabaseViewModelBase
{
    /// <summary>作ったスレッドで、UI スレッドのこと。変更の通知をここへ戻す</summary>
    private readonly SynchronizationContext _context;

    /// <summary>毎分の実行のたびに読み直すか</summary>
    private readonly bool _refreshEveryRun;

    /// <summary>ViewModel を作り、変更の購読を始める</summary>
    /// <param name="job">この画面の機能の仕事 (変更・毎分の実行を購読する)</param>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    /// <param name="monitor">毎分の監視で、接続の設定を保存した直後に、すぐ 1 回実行させる。再送のゲートにも使う</param>
    /// <param name="refreshEveryRun">毎分の実行のたびに読み直すか。リマインダー・送信設定の編集や、MmmTool からの新しい依頼を、すぐ反映したい一覧だけ true</param>
    /// <exception cref="InvalidOperationException">UI スレッド以外で作った。変更の通知を戻す先が無い。</exception>
    protected SendViewModelBase(IBatchJob job, IConnectionSettingsDialogService dialogs, BatchMonitor monitor, bool refreshEveryRun = false)
        : base(dialogs)
    {
        Job = job;
        Monitor = monitor;
        _refreshEveryRun = refreshEveryRun;
        _context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("ViewModel は UI スレッドで作ってください。変更の通知を、作ったスレッドへ戻して反映するためです。");
        Job.Changed += OnJobChanged;
        if (_refreshEveryRun)
        {
            Job.Ran += OnJobChanged;
        }
    }

    /// <summary>この画面の機能の仕事</summary>
    protected IBatchJob Job { get; }

    /// <summary>毎分の監視</summary>
    protected BatchMonitor Monitor { get; }

    /// <summary>購読をやめる</summary>
    public override void Dispose()
    {
        Job.Changed -= OnJobChanged;
        Job.Ran -= OnJobChanged;
        base.Dispose();
    }

    /// <summary>この画面から接続の設定の画面を開いて閉じたら、新しい設定で 1 回実行してから、読み直す</summary>
    /// <returns>実行と読み直しの完了を表すタスク</returns>
    /// <remarks>次の分を待たずに実行して、前の失敗の表示を最新にする。</remarks>
    protected override async Task OnConnectionSettingsOpenedByThisAsync()
    {
        await Monitor.RunNowAsync();
        await RefreshAsync();
    }

    /// <summary>仕事の結果が変わった (・毎分の実行が終わった)ら、UI スレッドで読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJobChanged(object? sender, EventArgs e)
        => _context.Post(_ => RefreshAsync().Forget(), null);
}
