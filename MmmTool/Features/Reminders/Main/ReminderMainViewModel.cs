using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Paths;
using MmmTool.Core.Reminders;

namespace MmmTool.Features.Reminders.Main;

/// <summary>リマインダーのメイン画面。今日の対象を並べ、対応状態 (未 / スヌーズ / 完了)を切り替える。</summary>
/// <remarks>
/// 未対応 (未・スヌーズ)を <see cref="Pending"/>、完了を <see cref="Done"/> に分け、どちらも時刻 → 参照番号の順に並べる。
/// 保存内容が変わったら (<see cref="ReminderService.Changed"/>)読み直し、日付が変わったら新しい日の対象に切り替える。
/// UI スレッドで作ること (変更の通知を作ったスレッドへ戻して反映するため)。画面を閉じたら <see cref="ReminderViewModelBase.Dispose"/> する。
/// </remarks>
public sealed partial class ReminderMainViewModel : ReminderViewModelBase
{
    /// <summary>時刻監視 (通知から開いたときのスヌーズ)</summary>
    private readonly ReminderMonitor _monitor;
    /// <summary>入力・一覧画面</summary>
    private readonly IReminderDialogService _dialogs;
    /// <summary>リンクを開く処理</summary>
    private readonly IPathOpener _opener;

    /// <summary>ViewModel を作る</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="monitor">リマインダーの時刻監視</param>
    /// <param name="dialogs">入力・一覧画面を開く</param>
    /// <param name="opener">リンクを開く処理</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderMainViewModel(ReminderService reminders, ReminderMonitor monitor, IReminderDialogService dialogs, IPathOpener opener, TimeProvider time)
        : base(reminders, time)
    {
        _monitor = monitor;
        _dialogs = dialogs;
        _opener = opener;
    }

    /// <summary>未対応 (未・スヌーズ)の行</summary>
    public ObservableCollection<ReminderTodayItem> Pending { get; } = [];

    /// <summary>完了の行</summary>
    public ObservableCollection<ReminderTodayItem> Done { get; } = [];

    /// <summary>完了の見出し (「完了 (件数)」)</summary>
    [ObservableProperty]
    public partial string DoneHeader { get; private set; } = "";

    /// <summary>完了が 1 件以上あるか (完了の欄を出すか)</summary>
    [ObservableProperty]
    public partial bool HasDone { get; private set; }

    /// <summary>今日の対象が 1 件も無いか (空のメッセージを出すか)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>発動済みで未対応のものをスヌーズにする (通知から開いたとき)</summary>
    /// <returns>スヌーズへの切り替えの完了を表すタスク</returns>
    public Task SnoozeTriggeredAsync() => RunAsync(_monitor.SnoozeTriggeredAsync);

    /// <summary>新規追加 (入力画面を開く)</summary>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task AddAsync() => _dialogs.ShowInputAsync(null);

    /// <summary>一覧画面を開く</summary>
    /// <returns>一覧画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task ShowListAsync() => _dialogs.ShowListAsync();

    /// <summary>リンクを開く</summary>
    /// <param name="item">リンクを開く行</param>
    /// <returns>リンクを開く処理の完了を表すタスク</returns>
    [RelayCommand]
    private async Task OpenLinkAsync(ReminderTodayItem item)
    {
        if (item.Source.Link is not { } link || string.IsNullOrWhiteSpace(link))
        {
            return;
        }

        try
        {
            await _opener.OpenAsync(link);
        }
        catch (PathOpenException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>編集 (入力画面を開く)</summary>
    /// <param name="item">編集する行</param>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task EditAsync(ReminderTodayItem item) => _dialogs.ShowInputAsync(item.Source);

    /// <summary>削除 (論理削除)</summary>
    /// <param name="item">削除する行</param>
    /// <returns>削除の完了を表すタスク</returns>
    [RelayCommand]
    private Task DeleteAsync(ReminderTodayItem item) => RunAsync(() => Reminders.DeleteAsync(item.Source.No));

    /// <summary>ユーザーが状態を切り替えたら、今日の状態として保存する</summary>
    /// <param name="item">状態を切り替えた行</param>
    /// <param name="status">新しい状態</param>
    /// <returns>保存の完了を表すタスク</returns>
    private Task SaveStatusAsync(ReminderTodayItem item, ReminderStatus status)
        => RunAsync(() => Reminders.SetStateAsync(item.No, ReminderDates.ToDateValue(Time.GetLocalNow().DateTime), status));

    /// <summary>保存に失敗したら、画面の状態を保存内容に戻すため読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    protected override Task OnSaveFailedAsync() => RefreshAsync();

    /// <summary>今日の対象を読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    protected override async Task RefreshAsync()
    {
        var version = NextVersion();
        var now = Time.GetLocalNow().DateTime;

        var targets = await Reminders.GetTargetsAsync(now);
        if (!IsCurrent(version))
        {
            return;
        }

        Sync(Pending, [.. targets.Where(target => target.Status != ReminderStatus.Done)]);
        Sync(Done, [.. targets.Where(target => target.Status == ReminderStatus.Done)]);

        HasDone = Done.Count > 0;
        DoneHeader = $"完了 ({Done.Count})";
        IsEmpty = targets.Count == 0;

        ScheduleDayChange(now);
    }

    /// <summary>行を並ぶべき内容に合わせる (差分更新)</summary>
    /// <param name="rows">画面に出している行</param>
    /// <param name="targets">並ぶべき内容 (リマインダーと今日の状態)</param>
    /// <remarks>
    /// 参照番号で既存の行を同定し、内容・状態を反映する。足りない行は挿入、余った行は削除、順番が違えば移動する。
    /// 全部を作り直さないのは、ちらつき・スクロール位置や完了の欄の開閉のリセットを防ぐため。
    /// </remarks>
    private void Sync(ObservableCollection<ReminderTodayItem> rows, IReadOnlyList<ReminderTarget> targets)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var (reminder, status) = targets[i];
            var index = IndexOf(rows, reminder.No, i);
            if (index < 0)
            {
                rows.Insert(i, new ReminderTodayItem(reminder, status, SaveStatusAsync));
                continue;
            }
            if (index != i)
            {
                rows.Move(index, i);
            }
            rows[i].Apply(reminder, status);
        }
        while (rows.Count > targets.Count)
        {
            rows.RemoveAt(rows.Count - 1);
        }
    }

    /// <summary>指定の位置以降で、参照番号が一致する行の位置。無ければ -1</summary>
    /// <param name="rows">画面に出している行</param>
    /// <param name="no">探す参照番号</param>
    /// <param name="start">探し始める位置</param>
    /// <returns>見つかった行の位置。無ければ -1</returns>
    private static int IndexOf(ObservableCollection<ReminderTodayItem> rows, int no, int start)
    {
        for (var i = start; i < rows.Count; i++)
        {
            if (rows[i].No == no)
            {
                return i;
            }
        }
        return -1;
    }
}
