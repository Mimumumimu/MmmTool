using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Storage;
using MmmSdk.WinUI.Errors;
using MmmTool.Core.Reminders;
using MmmSdk.Core.Tasks;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの画面（メイン画面・一覧画面）の ViewModel の共通部分</summary>
/// <remarks>
/// 保存内容が変わったら（<see cref="ReminderService.Changed"/>）UI スレッドで読み直す。読み込みの世代を管理し、古い読み込みの結果で上書きしない。
/// 保存を伴う操作（<see cref="RunAsync"/>）の失敗はエラーに出す。UI スレッドで作ること（変更の通知を作ったスレッドへ戻して反映するため）。
/// 画面を閉じたら <see cref="Dispose"/> で購読をやめる。
/// </remarks>
public abstract class ReminderViewModelBase : ObservableObject, IDisposable
{
    /// <summary>作ったスレッド（UI スレッド）。変更の通知をここへ戻す</summary>
    private readonly SynchronizationContext? _context;

    /// <summary>読み込みの世代。古い読み込みの結果で上書きしないためのもの</summary>
    private int _version;

    /// <summary>ViewModel を作り、保存内容の変更の購読を始める</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="time">現在時刻の提供元</param>
    protected ReminderViewModelBase(ReminderService reminders, TimeProvider time)
    {
        Reminders = reminders;
        Time = time;
        _context = SynchronizationContext.Current;
        Reminders.Changed += OnRemindersChanged;
    }

    /// <summary>エラー（読み込み・保存の失敗、壊れたファイルの退避など）</summary>
    public ErrorState Error { get; } = new();

    /// <summary>リマインダーの読み書き</summary>
    protected ReminderService Reminders { get; }

    /// <summary>現在時刻の提供元</summary>
    protected TimeProvider Time { get; }

    /// <summary>最初の読み込み</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>ファイルを読めなかった・壊れていたときは、そのことをエラーに出す。</remarks>
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        Error.Set(Reminders.LoadError ?? Reminders.RecoveryMessage);
    }

    /// <summary>購読をやめる</summary>
    public virtual void Dispose() => Reminders.Changed -= OnRemindersChanged;

    /// <summary>読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>始めに <see cref="NextVersion"/> で世代を取り、待ったあとに <see cref="IsCurrent"/> で確かめて、古ければ捨てる。</remarks>
    protected abstract Task RefreshAsync();

    /// <summary>新しい読み込みの世代を取る</summary>
    /// <returns>この読み込みの世代</returns>
    protected int NextVersion() => ++_version;

    /// <summary>世代が今も最新か</summary>
    /// <param name="version"><see cref="NextVersion"/> で取った世代</param>
    /// <returns>最新なら true（false なら、その読み込みの結果は捨てる）</returns>
    protected bool IsCurrent(int version) => version == _version;

    /// <summary>UI スレッドで読み直す</summary>
    /// <remarks>任意のスレッド（保存の通知・タイマー）から呼べる。</remarks>
    protected void PostRefresh()
    {
        if (_context is null)
        {
            RefreshAsync().Forget();
        }
        else
        {
            _context.Post(_ => RefreshAsync().Forget(), null);
        }
    }

    /// <summary>保存を伴う操作を行い、失敗したらエラーに出す</summary>
    /// <param name="action">行う操作</param>
    /// <returns>操作の完了を表すタスク</returns>
    protected async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            await OnSaveFailedAsync();
        }
    }

    /// <summary>保存に失敗したときの後始末（既定では何もしない）</summary>
    /// <returns>後始末の完了を表すタスク</returns>
    /// <remarks>保存内容と画面の状態がずれたとき、画面を保存内容に戻すために読み直す、などに使う。</remarks>
    protected virtual Task OnSaveFailedAsync() => Task.CompletedTask;

    /// <summary>保存内容が変わったら、UI スレッドで読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>任意のスレッドから来る。</remarks>
    private void OnRemindersChanged(object? sender, EventArgs e) => PostRefresh();
}
