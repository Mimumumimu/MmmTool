using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Notes.Core;

namespace MmmTool.Notes.Edit;

/// <summary>日程の 1 件の入力</summary>
/// <remarks>開始と終了の時刻は空にできる</remarks>
public sealed partial class ScheduleItemEditor : ObservableObject
{
    /// <summary>この予定を日付のまとまりから外す処理</summary>
    private readonly Action<ScheduleItemEditor> _remove;

    /// <summary>日程の 1 件の入力を作る</summary>
    /// <param name="remove">この予定を日付のまとまりから外す処理</param>
    /// <param name="item">読み込む日程で、新しい予定なら null。</param>
    public ScheduleItemEditor(Action<ScheduleItemEditor> remove, ScheduleItem? item = null)
    {
        _remove = remove;
        if (item is null)
        {
            return;
        }

        if (item.Start is { } start)
        {
            StartHour = start.Hour;
            StartMinute = start.Minute;
            StartIsEmpty = false;
        }
        if (item.End is { } end)
        {
            EndHour = end.Hour;
            EndMinute = end.Minute;
            EndIsEmpty = false;
        }
        Content = item.Content;
        Remark = item.Remark;
    }

    /// <summary>開始の時</summary>
    [ObservableProperty]
    public partial int StartHour { get; set; }

    /// <summary>開始の分</summary>
    [ObservableProperty]
    public partial int StartMinute { get; set; }

    /// <summary>開始が空か</summary>
    [ObservableProperty]
    public partial bool StartIsEmpty { get; set; } = true;

    /// <summary>終了の時</summary>
    [ObservableProperty]
    public partial int EndHour { get; set; }

    /// <summary>終了の分</summary>
    [ObservableProperty]
    public partial int EndMinute { get; set; }

    /// <summary>終了が空か</summary>
    [ObservableProperty]
    public partial bool EndIsEmpty { get; set; } = true;

    /// <summary>内容</summary>
    [ObservableProperty]
    public partial string Content { get; set; } = "";

    /// <summary>備考</summary>
    [ObservableProperty]
    public partial string Remark { get; set; } = "";

    /// <summary>何か入力してあるか</summary>
    /// <remarks>時刻・内容・備考のどれかが入っている</remarks>
    public bool HasInput => !StartIsEmpty || !EndIsEmpty || Content.Trim().Length > 0 || Remark.Trim().Length > 0;

    /// <summary>確認の画面に出す予定の要約</summary>
    /// <remarks>開始の時刻・内容・備考のうち入っているものを空白でつなぐ</remarks>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (!StartIsEmpty)
            {
                parts.Add($"{StartHour:00}:{StartMinute:00}");
            }
            if (Content.Trim().Length > 0)
            {
                parts.Add(Content.Trim());
            }
            if (Remark.Trim().Length > 0)
            {
                parts.Add(Remark.Trim());
            }
            return string.Join(' ', parts);
        }
    }

    /// <summary>この予定を削除する</summary>
    [RelayCommand]
    private void Remove() => _remove(this);

    /// <summary>日程の 1 件にする</summary>
    /// <param name="date">この予定の日付</param>
    /// <returns>日程の 1 件</returns>
    public ScheduleItem ToItem(DateOnly date)
        => new(
            date,
            StartIsEmpty ? null : new TimeOnly(StartHour, StartMinute),
            EndIsEmpty ? null : new TimeOnly(EndHour, EndMinute),
            Content,
            Remark);
}
