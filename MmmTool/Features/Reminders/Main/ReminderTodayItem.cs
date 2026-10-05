using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Utilities;
using MmmTool.Core.Reminders;

namespace MmmTool.Features.Reminders.Main;

/// <summary>リマインダーのメイン画面の行 (今日の対象 1 件)</summary>
/// <remarks>
/// 行は参照番号 (<see cref="No"/>)で同定し、読み直しでは作り直さずに <see cref="Apply"/> で中身を差し替える (ちらつき・スクロール位置のリセットを防ぐため)。
/// ユーザーが状態を切り替えたときだけ、作るときに渡した処理で保存する (読み直しによる反映では保存しない)。
/// </remarks>
public sealed partial class ReminderTodayItem : ObservableObject
{
    /// <summary>ユーザーが状態を切り替えたときの処理</summary>
    private readonly Func<ReminderTodayItem, ReminderStatus, Task> _statusChanged;

    /// <summary>読み直した内容を反映している最中か (このときは保存しない)</summary>
    private bool _applying;

    /// <summary>行を作る</summary>
    /// <param name="source">リマインダー</param>
    /// <param name="status">今日の対応状態</param>
    /// <param name="statusChanged">ユーザーが状態を切り替えたときの処理 (新しい状態を受け取る)</param>
    public ReminderTodayItem(Reminder source, ReminderStatus status, Func<ReminderTodayItem, ReminderStatus, Task> statusChanged)
    {
        _statusChanged = statusChanged;
        Source = source;
        Status = status;
    }

    /// <summary>参照番号 (行の同定に使う)</summary>
    public int No => Source.No;

    /// <summary>元のリマインダー</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText), nameof(Title), nameof(HasLink))]
    public partial Reminder Source { get; private set; }

    /// <summary>今日の対応状態</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIndex), nameof(IsDone))]
    public partial ReminderStatus Status { get; private set; }

    /// <summary>時刻 (HH:mm)</summary>
    public string TimeText => ReminderDates.ToTime(Source.Time)?.ToString("HH:mm") ?? "";

    /// <summary>件名</summary>
    public string Title => Source.Title;

    /// <summary>リンクがあるか</summary>
    public bool HasLink => !string.IsNullOrWhiteSpace(Source.Link);

    /// <summary>完了か</summary>
    public bool IsDone => Status == ReminderStatus.Done;

    /// <summary>状態の切り替えの選択位置 (0 未 / 1 スヌーズ / 2 完了)</summary>
    /// <remarks>画面の切り替え (セグメント)と双方向でつなぐ。ユーザーが切り替えたら保存する。</remarks>
    public int StatusIndex
    {
        get => Status switch
        {
            ReminderStatus.Snooze => 1,
            ReminderStatus.Done => 2,
            _ => 0,
        };
        set
        {
            var status = value switch
            {
                1 => ReminderStatus.Snooze,
                2 => ReminderStatus.Done,
                _ => ReminderStatus.None,
            };
            // 選択が外れた (-1)・同じ値・反映中は何もしない
            if (value < 0 || status == Status || _applying)
            {
                return;
            }
            Status = status;
            _statusChanged(this, status).Forget();
        }
    }

    /// <summary>読み直した内容を反映する (保存はしない)</summary>
    /// <param name="source">読み直したリマインダー</param>
    /// <param name="status">読み直した今日の対応状態</param>
    public void Apply(Reminder source, ReminderStatus status)
    {
        _applying = true;
        try
        {
            if (Source != source)
            {
                Source = source;
            }
            Status = status;
        }
        finally
        {
            _applying = false;
        }
    }
}
