using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Scheduling;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders;

/// <summary>リマインダーの画面 (メイン画面・一覧画面)の ViewModel の共通部分</summary>
/// <remarks>
/// 保存内容が変わったら (<see cref="ReminderService.Changed"/>)UI スレッドで読み直す。読み込みの世代を管理し、古い読み込みの結果で上書きしない。
/// 保存を伴う操作 (<see cref="RunAsync"/>)の失敗はエラーに出す。UI スレッドで作ること (変更の通知を作ったスレッドへ戻して反映するため)。
/// 画面を閉じたら <see cref="Dispose"/> で購読をやめる。
/// </remarks>
public abstract class ReminderViewModelBase : ObservableObject, IDisposable
{
    /// <summary>作ったスレッドである UI スレッド。変更の通知をここへ戻す</summary>
    private readonly SynchronizationContext _context;

    /// <summary>画面に出している日付と今の日付を毎分比べ、違っていたら読み直させる</summary>
    private readonly DayChangeWatcher _dayWatcher;

    /// <summary>直近の操作 (保存・リンクを開く)の失敗のメッセージ。無ければ null</summary>
    /// <remarks>読み込みの失敗と違い、保存先の状態からは組み立てられないので、次の操作が成功するまで覚えておく。</remarks>
    private string? _operationError;

    /// <summary>読み込みの世代。古い読み込みの結果で上書きしないためのもの</summary>
    private int _version;

    /// <summary>ViewModel を作り、保存内容の変更の購読を始める</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="time">現在時刻の提供元</param>
    /// <exception cref="InvalidOperationException">UI スレッド以外で作った (変更の通知を戻す先が無い)。</exception>
    protected ReminderViewModelBase(ReminderService reminders, TimeProvider time)
    {
        Reminders = reminders;
        Time = time;
        _context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("ViewModel は UI スレッドで作ってください (変更の通知を、作ったスレッドへ戻して反映するため)。");
        _dayWatcher = new DayChangeWatcher(time);
        Reminders.Changed += OnRemindersChanged;
    }

    /// <summary>エラー (読み込み・保存の失敗、壊れたファイルの退避など)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>リマインダーの読み書き</summary>
    protected ReminderService Reminders { get; }

    /// <summary>現在時刻の提供元</summary>
    protected TimeProvider Time { get; }

    /// <summary>最初の読み込み</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// ファイルを読めなかった・壊れていたときは、そのことをエラーに出す。
    /// 日付の監視は、最初の読み込みのあとに始める。読み込みの前に始めると、日付を覚える前に比べて、二重に読み込むため。
    /// </remarks>
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        UpdateError();
        _dayWatcher.Start(PostRefresh);
    }

    /// <summary>購読をやめ、日付の監視を止める</summary>
    public virtual void Dispose()
    {
        Reminders.Changed -= OnRemindersChanged;
        _dayWatcher.Dispose();
    }

    /// <summary>読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>始めに <see cref="NextVersion"/> で世代を取り、待ったあとに <see cref="IsCurrent"/> で確かめて、古ければ捨てる。</remarks>
    protected abstract Task RefreshAsync();

    /// <summary>新しい読み込みの世代を取る</summary>
    /// <returns>この読み込みの世代</returns>
    protected int NextVersion() => ++_version;

    /// <summary>世代が今も最新か</summary>
    /// <param name="version"><see cref="NextVersion"/> で取った世代</param>
    /// <returns>最新なら true (false なら、その読み込みの結果は捨てる)</returns>
    protected bool IsCurrent(int version) => version == _version;

    /// <summary>UI スレッドで読み直す</summary>
    /// <remarks>任意のスレッド (保存の通知・タイマー)から呼べる。</remarks>
    protected void PostRefresh() => _context.Post(_ => RefreshAndUpdateErrorAsync().Forget(), null);

    /// <summary>読み直して、エラーの表示を保存先の今の状態に合わせる</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>初回にログインより先に読み込んで失敗した表示が、ログイン後の読み直しで成功したら消えるようにする。</remarks>
    private async Task RefreshAndUpdateErrorAsync()
    {
        await RefreshAsync();
        UpdateError();
    }

    /// <summary>エラーの表示を、直近の操作の失敗と保存先の今の状態 (読み込みの失敗・退避の通知・時計のずれの警告)から組み立て直す</summary>
    private void UpdateError()
    {
        var messages = new[] { _operationError ?? Reminders.LoadError ?? Reminders.RecoveryMessage, Reminders.TimeWarning }.OfType<string>();
        Error.Set(string.Join("\n", messages) is { Length: > 0 } message ? message : null);
    }

    /// <summary>操作の失敗をエラーに出す</summary>
    /// <param name="message">表示するメッセージ</param>
    /// <remarks>次の操作が成功する (<see cref="RunAsync"/>)まで残る。</remarks>
    protected void ShowOperationError(string message)
    {
        _operationError = message;
        UpdateError();
    }

    /// <summary>画面に出した一覧が、何日の分かを覚える</summary>
    /// <param name="now">一覧を作るのに使った現在の日時</param>
    /// <remarks>
    /// 今日の対象・「過去」の判定が、開いたまま日付をまたいで古くならないようにする。毎分、今の日付とここで覚えた日付を比べ、違っていれば読み直す。
    /// 読み直し (<see cref="RefreshAsync"/>)で、古い読み込みとして捨てなかった結果を画面に反映した後に呼ぶ。
    /// </remarks>
    protected void MarkShown(DateTime now) => _dayWatcher.MarkShown(now);

    /// <summary>保存を伴う操作を行い、失敗したらエラーに出す</summary>
    /// <param name="action">行う操作</param>
    /// <returns>操作の完了を表すタスク</returns>
    protected async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            _operationError = null;
            UpdateError();
        }
        catch (DataFileException ex)
        {
            ShowOperationError(ex.Message);
            await OnSaveFailedAsync();
        }
    }

    /// <summary>保存に失敗したときの後始末 (既定では何もしない)</summary>
    /// <returns>後始末の完了を表すタスク</returns>
    /// <remarks>保存内容と画面の状態がずれたとき、画面を保存内容に戻すために読み直す、などに使う。</remarks>
    protected virtual Task OnSaveFailedAsync() => Task.CompletedTask;

    /// <summary>保存内容が変わったら、UI スレッドで読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>任意のスレッドから来る。</remarks>
    private void OnRemindersChanged(object? sender, EventArgs e) => PostRefresh();
}
