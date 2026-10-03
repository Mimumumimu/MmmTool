using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Repositories;
using MmmTool.Core.Services;
using MmmTool.Services;

namespace MmmTool.ViewModels;

/// <summary>
/// リマインダー一覧画面。全リマインダーを並べ、追加・編集・削除・コピーして新規追加・完全削除を行う。
/// </summary>
/// <remarks>
/// 並びは 日付 → 時刻 → 連番 の昇順（曜日指定は日付なしの特殊値なので日付指定の後ろに来る）。
/// 保存内容が変わったら（<see cref="ReminderService.Changed"/>）一覧を読み直す。UI スレッドで作ること（変更の通知を作ったスレッドへ戻して反映するため）。
/// 画面を閉じたら <see cref="Dispose"/> で購読をやめる。
/// </remarks>
public sealed partial class ReminderListViewModel : ObservableObject, IDisposable
{
    /// <summary>リマインダーの読み書き</summary>
    private readonly ReminderService _reminders;
    /// <summary>ダイアログ</summary>
    private readonly IDialogService _dialogs;
    /// <summary>現在日時（過去の予定の判定に使う）</summary>
    private readonly TimeProvider _time;
    /// <summary>作ったスレッド（UI スレッド）。変更の通知をここへ戻す</summary>
    private readonly SynchronizationContext? _context;

    /// <summary>読み込みの世代。古い読み込みの結果で上書きしないためのもの</summary>
    private int _version;

    /// <summary>ViewModel を作る</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="dialogs">ダイアログを開く</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderListViewModel(ReminderService reminders, IDialogService dialogs, TimeProvider time)
    {
        _reminders = reminders;
        _dialogs = dialogs;
        _time = time;
        _context = SynchronizationContext.Current;
        _reminders.Changed += OnRemindersChanged;
    }

    /// <summary>一覧の行</summary>
    public ObservableCollection<ReminderListItem> Items { get; } = [];

    /// <summary>論理削除済みも表示するか</summary>
    [ObservableProperty]
    public partial bool ShowDeleted { get; set; }

    /// <summary>過去の予定（日付が昨日以前の日付指定）も表示するか</summary>
    /// <remarks>既定はオフ（終わった予定が一覧の上に並んで邪魔になるため。ユーザー決定）。今日の分は時刻が過ぎていても表示する。</remarks>
    [ObservableProperty]
    public partial bool ShowPast { get; set; }

    /// <summary>エラー（読み込み・保存の失敗、壊れたファイルの退避）。無ければ null</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    /// <summary>エラーがあるか</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>最初の読み込み</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>ファイルを読めなかった・壊れていたときは、そのことをエラーに出す。</remarks>
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        ErrorMessage = _reminders.LoadError ?? _reminders.RecoveryMessage;
    }

    /// <summary>購読をやめる</summary>
    public void Dispose() => _reminders.Changed -= OnRemindersChanged;

    /// <summary>「削除済みを表示」が変わったら読み直す</summary>
    /// <param name="value">変更後の「削除済みを表示」</param>
    partial void OnShowDeletedChanged(bool value) => _ = RefreshAsync();

    /// <summary>「過去の予定を表示」が変わったら読み直す</summary>
    /// <param name="value">変更後の「過去の予定を表示」</param>
    partial void OnShowPastChanged(bool value) => _ = RefreshAsync();

    /// <summary>新規追加（入力画面を開く）</summary>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task AddAsync() => _dialogs.ShowReminderInputAsync(null);

    /// <summary>編集（入力画面を開く）</summary>
    /// <param name="item">編集する行</param>
    /// <returns>編集の完了を表すタスク</returns>
    /// <remarks>論理削除済みは、保存すると削除が取り消されることを確認してから開く。</remarks>
    [RelayCommand]
    private async Task EditAsync(ReminderListItem item)
    {
        if (item.IsDeleted
            && !await _dialogs.ConfirmAsync("削除済みのリマインダー", "このリマインダーは削除済みです。\n編集して保存すると、削除が取り消されます。", "編集する"))
        {
            return;
        }
        await _dialogs.ShowReminderInputAsync(item.Source);
    }

    /// <summary>コピーして新規追加（入力画面を開く）</summary>
    /// <param name="item">コピー元の行</param>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    /// <remarks>日付・時刻・曜日・件名・備考・リンクだけを引き継ぐ。連番・削除フラグは引き継がず、元のリマインダーは変えない。</remarks>
    [RelayCommand]
    private Task CopyAsNewAsync(ReminderListItem item)
        => _dialogs.ShowReminderInputAsync(item.Source with { Seq = 0, No = 0, IsDeleted = false });

    /// <summary>削除（論理削除）</summary>
    /// <param name="item">削除する行</param>
    /// <returns>削除の完了を表すタスク</returns>
    [RelayCommand]
    private Task DeleteAsync(ReminderListItem item) => RunAsync(() => _reminders.DeleteAsync(item.Source.Seq));

    /// <summary>完全削除（物理削除）</summary>
    /// <param name="item">完全削除する行</param>
    /// <returns>完全削除の完了を表すタスク</returns>
    /// <remarks>元に戻せないので確認してから。</remarks>
    [RelayCommand]
    private async Task PurgeAsync(ReminderListItem item)
    {
        if (await _dialogs.ConfirmAsync("完全削除", $"「{item.Title}」を完全に削除します。\nこの操作は元に戻せません。", "完全削除"))
        {
            await RunAsync(() => _reminders.PurgeAsync(item.Source.Seq));
        }
    }

    /// <summary>保存を伴う操作を行い、失敗したらエラーに出す</summary>
    /// <param name="action">行う操作</param>
    /// <returns>操作の完了を表すタスク</returns>
    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            ErrorMessage = null;
        }
        catch (DataFileException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>保存内容が変わったら、UI スレッドで読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>任意のスレッドから来る。</remarks>
    private void OnRemindersChanged(object? sender, EventArgs e)
    {
        if (_context is null)
        {
            _ = RefreshAsync();
        }
        else
        {
            _context.Post(_ => _ = RefreshAsync(), null);
        }
    }

    /// <summary>一覧を読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>変わった行だけを差し替える（全部を作り直すとスクロール位置が先頭へ戻るため）。</remarks>
    private async Task RefreshAsync()
    {
        var version = ++_version;
        var reminders = await _reminders.GetRemindersAsync(ShowDeleted);
        if (version != _version)
        {
            return;
        }

        // 曜日指定は日付なしの特殊値（99999999）なので、過去には入らない
        var today = ReminderDates.ToDateValue(_time.GetLocalNow().DateTime);
        var sorted = reminders.Where(item => ShowPast || item.Date >= today).OrderBy(item => item.Date).ThenBy(item => item.Time).ThenBy(item => item.Seq).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            if (i >= Items.Count)
            {
                Items.Add(new ReminderListItem(sorted[i]));
            }
            else if (Items[i].Source != sorted[i])
            {
                Items[i] = new ReminderListItem(sorted[i]);
            }
        }
        while (Items.Count > sorted.Count)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }
}
