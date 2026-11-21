using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Holidays;
using MmmTool.Notes.Core;

namespace MmmTool.Notes.Edit;

/// <summary>日程の 1 日ぶんのまとまり</summary>
/// <remarks>
/// 日付を 1 回だけ出し、その日の予定を時刻の順に並べずに、入力した順で持つ。
/// 保存するとき日付と開始の順に並べる。
/// 最後の予定を削除したら、まとまりごと外す。
/// </remarks>
public sealed class ScheduleDayGroup : ObservableObject
{
    /// <summary>まとまりを外す処理</summary>
    private readonly Action<ScheduleDayGroup> _removeGroup;

    /// <summary>日付を変える処理</summary>
    private readonly Action<ScheduleDayGroup, DateOnly> _moveDate;

    /// <summary>別の日付へコピーする処理</summary>
    private readonly Action<ScheduleDayGroup, DateOnly> _copyDate;

    /// <summary>日程の 1 日ぶんのまとまりを作る</summary>
    /// <param name="date">日付</param>
    /// <param name="removeGroup">まとまりを外す処理</param>
    /// <param name="moveDate">日付を変える処理</param>
    /// <param name="copyDate">別の日付へコピーする処理</param>
    public ScheduleDayGroup(DateOnly date, Action<ScheduleDayGroup> removeGroup, Action<ScheduleDayGroup, DateOnly> moveDate, Action<ScheduleDayGroup, DateOnly> copyDate)
    {
        Date = date;
        _removeGroup = removeGroup;
        _moveDate = moveDate;
        _copyDate = copyDate;
        AddItemCommand = new RelayCommand(() => AddItem());
    }

    /// <summary>日付</summary>
    public DateOnly Date { get; private set; }

    /// <summary>日曜か祝日か</summary>
    /// <remarks>
    /// 赤で示す
    /// 土曜が祝日のときも赤
    /// 日付の欄のカレンダーと同じ決まり
    /// </remarks>
    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday || JapaneseHolidays.IsHoliday(Date);

    /// <summary>土曜か</summary>
    /// <remarks>祝日の土曜は <see cref="IsSunday"/> が true で、こちらは false。</remarks>
    public bool IsSaturday => !IsSunday && Date.DayOfWeek == DayOfWeek.Saturday;

    /// <summary>土曜でも日曜でも祝日でもないか</summary>
    public bool IsPlainDay => !IsSunday && !IsSaturday;

    /// <summary>日付の表示で、月日と曜日</summary>
    public string DateText => Date.ToString("MM/dd", CultureInfo.InvariantCulture) + " (" + new CultureInfo("ja-JP").DateTimeFormat.GetAbbreviatedDayName(Date.DayOfWeek) + ")";

    /// <summary>この日の予定ごと、別の日付へ移す</summary>
    /// <param name="date">移す先の日付</param>
    public void MoveTo(DateOnly date)
    {
        if (date != Date)
        {
            _moveDate(this, date);
        }
    }

    /// <summary>この日の予定を別の日付へコピーする</summary>
    /// <param name="date">コピー先の日付</param>
    public void CopyTo(DateOnly date)
    {
        if (date != Date)
        {
            _copyDate(this, date);
        }
    }

    /// <summary>日付を変える</summary>
    /// <param name="date">新しい日付</param>
    public void SetDate(DateOnly date)
    {
        Date = date;
        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(IsSunday));
        OnPropertyChanged(nameof(IsSaturday));
        OnPropertyChanged(nameof(IsPlainDay));
    }

    /// <summary>この日の予定</summary>
    public ObservableCollection<ScheduleItemEditor> Items { get; } = [];

    /// <summary>この日に予定を追加する命令</summary>
    public IRelayCommand AddItemCommand { get; }

    /// <summary>予定を足す</summary>
    /// <param name="item">読み込む日程で、空の予定なら null。</param>
    /// <returns>足した予定</returns>
    public ScheduleItemEditor AddItem(ScheduleItem? item = null)
    {
        var editor = new ScheduleItemEditor(Remove, item);
        Items.Add(editor);
        return editor;
    }

    /// <summary>予定を外す</summary>
    /// <param name="item">外す予定</param>
    /// <remarks>最後の予定を外したら、まとまりごと外す。</remarks>
    private void Remove(ScheduleItemEditor item)
    {
        Items.Remove(item);
        if (Items.Count == 0)
        {
            _removeGroup(this);
        }
    }

    /// <summary>日程にする</summary>
    /// <returns>
    /// 入力のある予定の日程。
    /// 入力のある予定が 1 つも無いときは、日付だけの日程を 1 件。
    /// </returns>
    /// <remarks>日付だけで足りるので、空の予定は、ほかに予定があるときに限って、捨てる。</remarks>
    public IEnumerable<ScheduleItem> ToItems()
    {
        var filled = Items.Where(item => item.HasInput).ToList();
        if (filled.Count == 0)
        {
            return [ScheduleItem.OfDate(Date)];
        }
        return filled.Select(item => item.ToItem(Date));
    }
}
