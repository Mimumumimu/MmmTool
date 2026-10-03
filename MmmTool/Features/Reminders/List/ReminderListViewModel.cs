using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.WinUI.Dialogs;
using MmmTool.Core.Reminders;
using MmmSdk.Core.Tasks;

namespace MmmTool.Features.Reminders.List;

/// <summary>
/// リマインダー一覧画面。全リマインダーを並べ、追加・編集・削除・コピーして新規追加・完全削除を行う。
/// </summary>
/// <remarks>
/// 並びは 日付 → 時刻 → 番号 の昇順（曜日指定は日付なしの特殊値なので日付指定の後ろに来る）。
/// 保存内容が変わったら（<see cref="ReminderService.Changed"/>）一覧を読み直す。UI スレッドで作ること（変更の通知を作ったスレッドへ戻して反映するため）。
/// 画面を閉じたら <see cref="ReminderViewModelBase.Dispose"/> で購読をやめる。
/// </remarks>
public sealed partial class ReminderListViewModel : ReminderViewModelBase
{
    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _dialogs;
    /// <summary>入力画面</summary>
    private readonly IReminderDialogService _reminderDialogs;

    /// <summary>ViewModel を作る</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="dialogs">確認ダイアログを開く</param>
    /// <param name="reminderDialogs">入力画面を開く</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderListViewModel(ReminderService reminders, IDialogService dialogs, IReminderDialogService reminderDialogs, TimeProvider time)
        : base(reminders, time)
    {
        _dialogs = dialogs;
        _reminderDialogs = reminderDialogs;
    }

    /// <summary>一覧の行</summary>
    public ObservableCollection<ReminderListItem> Items { get; } = [];

    /// <summary>論理削除済みも表示するか</summary>
    [ObservableProperty]
    public partial bool ShowDeleted { get; set; }

    /// <summary>過去の予定（日付が昨日以前の日付指定）も表示するか</summary>
    /// <remarks>既定はオフ（終わった予定が一覧の上に並んで邪魔になるため）。今日の分は時刻が過ぎていても表示する。</remarks>
    [ObservableProperty]
    public partial bool ShowPast { get; set; }

    /// <summary>「削除済みを表示」が変わったら読み直す</summary>
    /// <param name="value">変更後の「削除済みを表示」</param>
    partial void OnShowDeletedChanged(bool value) => RefreshAsync().Forget();

    /// <summary>「過去の予定を表示」が変わったら読み直す</summary>
    /// <param name="value">変更後の「過去の予定を表示」</param>
    partial void OnShowPastChanged(bool value) => RefreshAsync().Forget();

    /// <summary>新規追加（入力画面を開く）</summary>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task AddAsync() => _reminderDialogs.ShowInputAsync(null);

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
        await _reminderDialogs.ShowInputAsync(item.Source);
    }

    /// <summary>コピーして新規追加（入力画面を開く）</summary>
    /// <param name="item">コピー元の行</param>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    /// <remarks>日付・時刻・曜日・件名・備考・リンクだけを引き継ぐ。番号・削除フラグは引き継がず、元のリマインダーは変えない。</remarks>
    [RelayCommand]
    private Task CopyAsNewAsync(ReminderListItem item)
        => _reminderDialogs.ShowInputAsync(item.Source with { No = 0, IsDeleted = false });

    /// <summary>削除（論理削除）</summary>
    /// <param name="item">削除する行</param>
    /// <returns>削除の完了を表すタスク</returns>
    [RelayCommand]
    private Task DeleteAsync(ReminderListItem item) => RunAsync(() => Reminders.DeleteAsync(item.Source.No));

    /// <summary>完全削除（物理削除）</summary>
    /// <param name="item">完全削除する行</param>
    /// <returns>完全削除の完了を表すタスク</returns>
    /// <remarks>元に戻せないので確認してから。</remarks>
    [RelayCommand]
    private async Task PurgeAsync(ReminderListItem item)
    {
        if (await _dialogs.ConfirmAsync("完全削除", $"「{item.Title}」を完全に削除します。\nこの操作は元に戻せません。", "完全削除"))
        {
            await RunAsync(() => Reminders.PurgeAsync(item.Source.No));
        }
    }

    /// <summary>一覧を読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>変わった行だけを差し替える（全部を作り直すとスクロール位置が先頭へ戻るため）。</remarks>
    protected override async Task RefreshAsync()
    {
        var version = NextVersion();
        var reminders = await Reminders.GetRemindersAsync(ShowDeleted);
        if (!IsCurrent(version))
        {
            return;
        }

        // 曜日指定は日付なしの特殊値（99999999）なので、過去には入らない
        var today = ReminderDates.ToDateValue(Time.GetLocalNow().DateTime);
        var sorted = reminders.Where(item => ShowPast || item.Date >= today).OrderBy(item => item.Date).ThenBy(item => item.Time).ThenBy(item => item.No).ToList();
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
