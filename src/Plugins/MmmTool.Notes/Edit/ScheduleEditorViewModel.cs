using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Notes.Core;

namespace MmmTool.Notes.Edit;

/// <summary>日程の入力</summary>
/// <remarks>
/// 日付ごとにまとまりを持ち、日付の順に並べる。
/// 日付は、複数選択のカレンダーで足す・外す。
/// 日付を足すと、予定が 1 つ入った、空のまとまりができる。
/// </remarks>
public sealed partial class ScheduleEditorViewModel : ObservableObject
{
    /// <summary>日付のまとまり</summary>
    public ObservableCollection<ScheduleDayGroup> Days { get; } = [];

    /// <summary>日程が 1 つも無いか</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>日付を足した</summary>
    /// <remarks>期間の外のとき、開始日・終了日を動かして合わせるため、日付を知らせる。</remarks>
    public event EventHandler<DateOnly>? DateAdded;

    /// <summary>日付が増減した</summary>
    public event EventHandler? DatesChanged;

    /// <summary>入力してある日付</summary>
    public IReadOnlyList<DateOnly> Dates => [.. Days.Select(group => group.Date)];

    /// <summary>日程を渡された内容で置き換える</summary>
    /// <param name="items">日程で、日付と開始の順。</param>
    /// <remarks>読み込むとき。通知は出さない。</remarks>
    public void Load(IEnumerable<ScheduleItem> items)
    {
        Days.Clear();
        foreach (var group in items.GroupBy(item => item.Date).OrderBy(group => group.Key))
        {
            var day = new ScheduleDayGroup(group.Key, RemoveGroup, MoveDate, CopyDate);
            foreach (var item in group)
            {
                day.AddItem(item);
            }
            Days.Add(day);
        }
        IsEmpty = Days.Count == 0;
    }

    /// <summary>入力を日程にする</summary>
    /// <returns>日程で、日付の順。</returns>
    public IReadOnlyList<ScheduleItem> ToItems() => [.. Days.SelectMany(group => group.ToItems())];

    /// <summary>日付のまとまりを足す</summary>
    /// <param name="day">足す日付</param>
    /// <remarks>
    /// 既にある日付は足さない
    /// 日付の順に入れる
    /// </remarks>
    public void AddDate(DateOnly day)
    {
        if (Days.Any(group => group.Date == day))
        {
            return;
        }

        var group = new ScheduleDayGroup(day, RemoveGroup, MoveDate, CopyDate);
        group.AddItem();
        InsertGroup(group);
        DateAdded?.Invoke(this, day);
        DatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>日付のまとまりを日付の順の位置に入れる</summary>
    /// <param name="group">入れるまとまり</param>
    private void InsertGroup(ScheduleDayGroup group)
    {
        var index = 0;
        while (index < Days.Count && Days[index].Date < group.Date)
        {
            index++;
        }
        Days.Insert(index, group);
        IsEmpty = false;
    }

    /// <summary>日付のまとまりの予定を別の日付へコピーする</summary>
    /// <param name="group">コピー元のまとまり</param>
    /// <param name="day">コピー先の日付</param>
    /// <remarks>
    /// 入力のある予定だけをコピーして元は残す
    /// コピー先にまとまりが既にあれば下に足し無ければ新しく作る
    /// 入力のある予定が無いときは空の予定が 1 つの新しいまとまりを作る
    /// </remarks>
    private void CopyDate(ScheduleDayGroup group, DateOnly day)
    {
        var copies = group.Items.Where(item => item.HasInput).Select(item => item.ToItem(day)).ToList();
        if (Days.FirstOrDefault(other => other.Date == day) is { } target)
        {
            foreach (var copy in copies)
            {
                target.AddItem(copy);
            }
        }
        else
        {
            var created = new ScheduleDayGroup(day, RemoveGroup, MoveDate, CopyDate);
            if (copies.Count == 0)
            {
                created.AddItem();
            }
            foreach (var copy in copies)
            {
                created.AddItem(copy);
            }
            InsertGroup(created);
        }
        DateAdded?.Invoke(this, day);
        DatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>日付のまとまりを外す</summary>
    /// <param name="day">外す日付</param>
    public void RemoveDate(DateOnly day)
    {
        if (Days.FirstOrDefault(group => group.Date == day) is { } group)
        {
            RemoveGroup(group);
        }
    }

    /// <summary>その日に入力があるか</summary>
    /// <param name="day">日付</param>
    /// <returns>時刻・内容・備考のどれかを入れた予定があれば true</returns>
    public bool HasInput(DateOnly day) => Days.FirstOrDefault(group => group.Date == day)?.Items.Any(item => item.HasInput) == true;

    /// <summary>日付のまとまりの日付を変える</summary>
    /// <param name="group">日付を変えるまとまり</param>
    /// <param name="day">新しい日付</param>
    /// <remarks>
    /// 新しい日付に既にまとまりがあれば、空の予定も含めて全部そこへ足して、元のまとまりを外す。
    /// 無ければ、まとまりの日付を変えて、日付の順の位置へ動かす。
    /// </remarks>
    private void MoveDate(ScheduleDayGroup group, DateOnly day)
    {
        if (Days.FirstOrDefault(other => other.Date == day) is { } target)
        {
            foreach (var item in group.Items)
            {
                target.AddItem(item.ToItem(day));
            }
            Days.Remove(group);
            IsEmpty = Days.Count == 0;
        }
        else
        {
            group.SetDate(day);
            var from = Days.IndexOf(group);
            var to = 0;
            foreach (var other in Days.Where(other => other != group))
            {
                if (other.Date < day)
                {
                    to++;
                }
            }
            Days.Move(from, to);
        }
        DateAdded?.Invoke(this, day);
        DatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>日付のまとまりを外す</summary>
    /// <param name="group">外すまとまり</param>
    private void RemoveGroup(ScheduleDayGroup group)
    {
        Days.Remove(group);
        IsEmpty = Days.Count == 0;
        DatesChanged?.Invoke(this, EventArgs.Empty);
    }
}
