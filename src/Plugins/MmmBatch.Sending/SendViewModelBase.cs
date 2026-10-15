using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;

namespace MmmBatch.Sending;

/// <summary>
/// 送信の一覧の画面 (今日の送信予定・最近の依頼・履歴)の ViewModel の共通部分。
/// </summary>
/// <remarks>
/// 機能の仕事 (<see cref="IBatchJob"/>)の結果が変わったら (<see cref="IBatchJob.Changed"/>)、UI スレッドで読み直す。読み込みの世代を管理し、古い読み込みの結果で上書きしない。
/// 読み込み・送信の失敗 (接続できない・表が無い)は <see cref="Error"/> に出し、「接続の設定」から、接続の設定の画面を開ける。UI スレッドで作ること (変更の通知を作ったスレッドへ戻して反映するため)。
/// </remarks>
public abstract partial class SendViewModelBase : ObservableObject, IDisposable
{
    /// <summary>接続の設定の画面を開く</summary>
    private readonly ISendingDialogService _dialogs;

    /// <summary>作ったスレッド (UI スレッド)。変更の通知をここへ戻す</summary>
    private readonly SynchronizationContext _context;

    /// <summary>毎分の実行のたびに読み直すか</summary>
    private readonly bool _refreshEveryRun;

    /// <summary>読み込みの世代。古い読み込みの結果で上書きしないためのもの</summary>
    private int _version;

    /// <summary>ViewModel を作り、変更の購読を始める</summary>
    /// <param name="job">この画面の機能の仕事 (変更・毎分の実行を購読する)</param>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    /// <param name="monitor">毎分の監視 (接続の設定を保存した直後に、すぐ 1 回実行させる。再送のゲートにも使う)</param>
    /// <param name="refreshEveryRun">毎分の実行のたびに読み直すか (リマインダー・送信設定の編集や、MmmTool からの新しい依頼を、すぐ反映したい一覧だけ true)</param>
    /// <exception cref="InvalidOperationException">UI スレッド以外で作った (変更の通知を戻す先が無い)。</exception>
    protected SendViewModelBase(IBatchJob job, ISendingDialogService dialogs, BatchMonitor monitor, bool refreshEveryRun = false)
    {
        Job = job;
        Monitor = monitor;
        _dialogs = dialogs;
        _refreshEveryRun = refreshEveryRun;
        _context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("ViewModel は UI スレッドで作ってください (変更の通知を、作ったスレッドへ戻して反映するため)。");
        Job.Changed += OnJobChanged;
        if (_refreshEveryRun)
        {
            Job.Ran += OnJobChanged;
        }
    }

    /// <summary>読み込み・送信の失敗のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>この画面の機能の仕事</summary>
    protected IBatchJob Job { get; }

    /// <summary>毎分の監視</summary>
    protected BatchMonitor Monitor { get; }

    /// <summary>一覧を読み込む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public abstract Task RefreshAsync();

    /// <summary>購読をやめる</summary>
    public virtual void Dispose()
    {
        Job.Changed -= OnJobChanged;
        Job.Ran -= OnJobChanged;
    }

    /// <summary>新しい読み込みを始める (これより前に始めた読み込みの結果は、捨てる)</summary>
    /// <returns>この読み込みの世代</returns>
    protected int NextVersion() => Interlocked.Increment(ref _version);

    /// <summary>この読み込みが、いちばん新しいか</summary>
    /// <param name="version">読み込みを始めたときの世代</param>
    /// <returns>いちばん新しければ true</returns>
    protected bool IsCurrent(int version) => version == Volatile.Read(ref _version);

    /// <summary>仕事の結果が変わった (・毎分の実行が終わった)ら、UI スレッドで読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJobChanged(object? sender, EventArgs e)
        => _context.Post(_ => RefreshAsync().Forget(), null);

    /// <summary>接続の設定の画面を開き、閉じたら読み直す</summary>
    /// <returns>画面が閉じて、読み直すまでの待機を表すタスク</returns>
    [RelayCommand]
    private async Task OpenConnectionSettingsAsync()
    {
        await _dialogs.ShowConnectionSettingsAsync();
        // 次の分を待たずに、新しい設定で 1 回実行して、前の失敗の表示を最新にする
        await Monitor.RunNowAsync();
        await RefreshAsync();
    }
}
