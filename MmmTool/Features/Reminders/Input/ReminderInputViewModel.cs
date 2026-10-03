using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Storage;
using MmmSdk.WinUI.Errors;
using MmmTool.Core.Reminders;

namespace MmmTool.Features.Reminders.Input;

/// <summary>
/// リマインダー入力画面。リマインダー 1 件を新規登録・編集して保存する。
/// </summary>
/// <remarks>
/// 発動の指定は「日付を指定する」のオン（日付指定）とオフ（曜日指定）の 2 通り。曜日を 1 つも選ばない曜日指定は毎日通知になる。
/// 保存に成功したら <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class ReminderInputViewModel : ObservableObject
{
    /// <summary>件名が未入力のときのエラー</summary>
    private const string TitleRequiredMessage = "件名を入力してください。";

    /// <summary>リマインダーの読み書き</summary>
    private readonly ReminderService _reminders;
    /// <summary>現在日時</summary>
    private readonly TimeProvider _time;

    /// <summary>編集対象。新規なら null</summary>
    private Reminder? _target;

    /// <summary>ViewModel を作る</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderInputViewModel(ReminderService reminders, TimeProvider time)
    {
        _reminders = reminders;
        _time = time;
        WeekdayOptions = [.. ReminderDates.WeekdayNames.Select(weekday => new WeekdayOption(weekday.Flag, weekday.Name))];
        Load(null);
    }

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>保存したときは保存した内容、キャンセルのときは null を渡す。</remarks>
    public event EventHandler<Reminder?>? CloseRequested;

    /// <summary>画面のタイトル</summary>
    /// <remarks>新規は「リマインダー入力」、編集（採番済み）は「リマインダー編集」。</remarks>
    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = "";

    /// <summary>件名</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>件名のエラー。無ければ null</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitleError))]
    public partial string? TitleError { get; private set; }

    /// <summary>件名のエラーがあるか</summary>
    public bool HasTitleError => TitleError is not null;

    /// <summary>日付を指定するか（オフなら曜日指定）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekdaySpecified))]
    public partial bool IsDateSpecified { get; set; }

    /// <summary>曜日指定か（<see cref="IsDateSpecified"/> の逆）</summary>
    public bool IsWeekdaySpecified => !IsDateSpecified;

    /// <summary>発動日（日付指定のとき）</summary>
    [ObservableProperty]
    public partial DateTimeOffset? Date { get; set; }

    /// <summary>曜日の選択肢（月〜日）</summary>
    public IReadOnlyList<WeekdayOption> WeekdayOptions { get; }

    /// <summary>発動時刻の時（0〜23）</summary>
    [ObservableProperty]
    public partial int Hour { get; set; }

    /// <summary>発動時刻の分（0〜59）</summary>
    [ObservableProperty]
    public partial int Minute { get; set; }

    /// <summary>備考</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = "";

    /// <summary>リンク（URL・ファイル・フォルダのパス）</summary>
    [ObservableProperty]
    public partial string Link { get; set; } = "";

    /// <summary>保存のエラー</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>入力欄に読み込む</summary>
    /// <param name="target">編集するリマインダー。新規なら null</param>
    /// <remarks>新規は 日付＝今日・時刻＝現在時刻・曜日指定（曜日は未選択）・ほかは空。編集は対象の値を読み込む（曜日指定なら日付欄は今日）。</remarks>
    public void Load(Reminder? target)
    {
        _target = target;
        var now = _time.GetLocalNow();
        var today = new DateTimeOffset(now.Date, now.Offset);

        WindowTitle = target is { Seq: > 0 } ? "リマインダー編集" : "リマインダー入力";
        Title = target?.Title ?? "";
        Note = target?.Note ?? "";
        Link = target?.Link ?? "";

        if (target is null)
        {
            IsDateSpecified = false;
            Date = today;
            SetWeekdays(Weekdays.None);
            (Hour, Minute) = (now.Hour, now.Minute);
        }
        else
        {
            var date = ReminderDates.ToDate(target.Date);
            IsDateSpecified = !ReminderDates.IsWeekdaySpecified(target.Date);
            Date = date is { } value ? new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue), now.Offset) : today;
            SetWeekdays(IsDateSpecified ? Weekdays.None : target.Weekdays);
            var time = ReminderDates.ToTime(target.Time) ?? TimeOnly.MinValue;
            (Hour, Minute) = (time.Hour, time.Minute);
        }

        // 読み込みで件名が変わってもエラーは出さない（エラーは保存しようとしたときだけ）
        TitleError = null;
        SaveError.Clear();
    }

    /// <summary>件名が変わったら、件名のエラーを消す</summary>
    /// <param name="value">変更後の件名</param>
    partial void OnTitleChanged(string value) => TitleError = null;

    /// <summary>日付が空にされたら、元の日付に戻す</summary>
    /// <param name="oldValue">変更前の日付</param>
    /// <param name="newValue">変更後の日付</param>
    /// <remarks>カレンダーで選択中の日付をもう一度押すと選択が外れるため。日付指定は常に日付を持つ。</remarks>
    partial void OnDateChanged(DateTimeOffset? oldValue, DateTimeOffset? newValue)
    {
        if (newValue is null && oldValue is not null)
        {
            Date = oldValue;
        }
    }

    /// <summary>入力値を検証して保存し、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>件名が未入力ならエラーを出して保存しない。保存に失敗したらエラーを出して閉じない。</remarks>
    [RelayCommand]
    private async Task SaveAsync()
    {
        SaveError.Clear();
        var title = Title.Trim();
        if (title.Length == 0)
        {
            TitleError = TitleRequiredMessage;
            return;
        }

        var reminder = (_target ?? new Reminder()) with
        {
            Title = title,
            Date = IsDateSpecified && Date is { } date ? ReminderDates.ToDateValue(date.DateTime) : ReminderDates.NoDate,
            Weekdays = IsDateSpecified ? Weekdays.None : GetWeekdays(),
            Time = ReminderDates.ToTimeValue(new TimeOnly(Math.Clamp(Hour, 0, 23), Math.Clamp(Minute, 0, 59))),
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note,
            Link = string.IsNullOrWhiteSpace(Link) ? null : Link.Trim(),
        };

        try
        {
            var saved = await _reminders.SaveAsync(reminder);
            CloseRequested?.Invoke(this, saved);
        }
        catch (Exception ex) when (ex is DataFileException or ArgumentException)
        {
            // ArgumentException は、編集中に対象が完全に削除されたとき
            SaveError.Show(ex.Message);
        }
    }

    /// <summary>保存せずに閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);

    /// <summary>曜日の選択肢を、曜日フラグに合わせる</summary>
    /// <param name="weekdays">反映する曜日フラグ</param>
    private void SetWeekdays(Weekdays weekdays)
    {
        foreach (var option in WeekdayOptions)
        {
            option.IsChecked = (weekdays & option.Flag) != 0;
        }
    }

    /// <summary>選ばれている曜日を曜日フラグにする</summary>
    /// <returns>選ばれている曜日の曜日フラグ</returns>
    private Weekdays GetWeekdays()
        => WeekdayOptions.Where(option => option.IsChecked).Aggregate(Weekdays.None, (all, option) => all | option.Flag);
}
