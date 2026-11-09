using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Notes.Core;

namespace MmmTool.Notes.Edit;

/// <summary>
/// ノートの入力画面。ノート 1 件を新規登録・編集して保存する。
/// </summary>
/// <param name="notes">ノートの読み書き</param>
/// <remarks>
/// 開始日と終了日は、一方を変えて前後が逆になったら、もう一方を同じ日に合わせる。終了日は、空にできて、空は無期限。
/// 実施日は、空にできて、空は未設定。期間の外に入れたら、開始日・終了日を動かして合わせる。終了日が空のときは、終了日は動かさない。
/// ほかの実施日 (複数)も同じ決まりで、期間の外に入れたら期間を合わせ、期間を動かして外れたときは、動かさず、警告を出して保存できなくする。
/// 開始日・終了日を動かして、実施日かほかの実施日が期間の外に出たときは、その日を動かさず、警告を出して保存できなくする。
/// 同じ日は、先に入っていた日が勝つ。実施日を、ほかの実施日にある日にしようとしたときは、実施日を元の日に戻して知らせる。
/// 公開範囲を変えられるのは、書いた人だけ。ほかの人が、全員に見えるノートを、自分だけのものにして隠さないため。
/// 保存に成功したら <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class NoteEditViewModel(NoteService notes) : ObservableObject
{
    /// <summary>重要度の選択の位置 (最低 = 0 … 最高 = 4)と、<see cref="NotePriority"/> の差</summary>
    private const int PriorityOffset = 1;

    /// <summary>編集対象。新規なら null</summary>
    private Note? _target;

    /// <summary>開始日・終了日をそろえている最中か (そろえた変更が、さらに変更を呼ばないように)</summary>
    private bool _syncingDates;

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>保存したときは true、キャンセルのときは false を渡す。</remarks>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>複数の人で共有するか。DB モードで、公開範囲を出す</summary>
    public bool IsShared => notes.IsShared;

    /// <summary>公開範囲を変えられるか (共有するときの、新規か、書いた人の編集)</summary>
    public bool CanChangeVisibility => IsShared && (_target is null || _target.AuthorUserId == notes.CurrentUserId);

    /// <summary>画面のタイトル</summary>
    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = "";

    /// <summary>タイトル</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Title { get; set; } = "";

    /// <summary>本文で、書いたとおりの文字。右クリックの助けが、書式のタグを入れる</summary>
    [ObservableProperty]
    public partial string Body { get; set; } = "";

    /// <summary>開始日</summary>
    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    /// <summary>終了日。空は無期限</summary>
    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    /// <summary>実施日。空は未設定</summary>
    [ObservableProperty]
    public partial DateTimeOffset? EventDate { get; set; }

    /// <summary>ほかの実施日の一覧で、日付の順に並ぶ。「✕」で 1 行ずつ外せる</summary>
    public ObservableCollection<OtherEventDateItem> OtherEventDates { get; } = [];

    /// <summary>ほかの実施日が 1 つも無いか (一覧の枠に「なし」を出す)</summary>
    [ObservableProperty]
    public partial bool IsOtherEventDatesEmpty { get; private set; } = true;

    /// <summary>実施日か、ほかの実施日が期間の外にあるという警告を出しているか (出している間は保存できない)</summary>
    [ObservableProperty]
    public partial bool IsPeriodErrorOpen { get; private set; }

    /// <summary>実施日が期間の外にあるか。実施日の欄の枠を、エラーの色にする</summary>
    [ObservableProperty]
    public partial bool IsEventDateInvalid { get; private set; }

    /// <summary>期間の外の警告の文</summary>
    [ObservableProperty]
    public partial string PeriodErrorMessage { get; private set; } = "";

    /// <summary>開始日・終了日を動かして合わせたことの知らせ</summary>
    public ErrorState AdjustInfo { get; } = new();

    /// <summary>重要度の選択の位置で、最低が 0、最高が 4。初期は「中」</summary>
    [ObservableProperty]
    public partial int PriorityIndex { get; set; } = (int)NotePriority.Normal - PriorityOffset;

    /// <summary>自分だけに表示するか。false は全員で、DB モードだけ</summary>
    [ObservableProperty]
    public partial bool IsPrivate { get; set; }

    /// <summary>保存の失敗</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>新規の登録・編集の内容を読み込む</summary>
    /// <param name="note">編集するノート。新規なら null</param>
    /// <param name="defaultStartDate">新規のときの、開始日の初期値</param>
    /// <remarks>新規の終了日は、開始日と同じで、1 日だけ。無期限は、利用者が空にして選ぶ。</remarks>
    public void Load(Note? note, DateOnly defaultStartDate)
    {
        _target = note;
        SaveError.Clear();
        AdjustInfo.Clear();
        _syncingDates = true;
        try
        {
            if (note is null)
            {
                WindowTitle = "ノート入力";
                Title = "";
                Body = "";
                StartDate = ToOffset(defaultStartDate);
                EndDate = null;
                EventDate = null;
                SetOtherEventDates([]);
                PriorityIndex = (int)NotePriority.Normal - PriorityOffset;
                IsPrivate = false;
            }
            else
            {
                WindowTitle = "ノート編集";
                Title = note.Title;
                Body = note.Body;
                StartDate = ToOffset(note.StartDate);
                EndDate = note.IsOpenEnded ? null : ToOffset(note.EndDate);
                EventDate = note.HasEventDate ? ToOffset(note.EventDate) : null;
                SetOtherEventDates(note.OtherEventDates);
                PriorityIndex = (int)note.Priority - PriorityOffset;
                IsPrivate = note.IsPrivate;
            }
        }
        finally
        {
            _syncingDates = false;
        }
        OnPropertyChanged(nameof(CanChangeVisibility));
        UpdatePeriodError();
    }

    /// <summary>開始日を変えたら、終了日より後にならないよう、終了日を合わせる</summary>
    /// <param name="value">新しい開始日</param>
    /// <remarks>実施日は動かさない。期間の外に出たときは、警告を出す。</remarks>
    partial void OnStartDateChanged(DateTimeOffset? value)
    {
        if (!_syncingDates)
        {
            AdjustInfo.Clear();
            if (value is { } start && EndDate is { } end && start.Date > end.Date)
            {
                _syncingDates = true;
                EndDate = start;
                _syncingDates = false;
            }
        }
        UpdatePeriodError();
    }

    /// <summary>終了日を変えたら、開始日より前にならないよう、開始日を合わせる</summary>
    /// <param name="value">新しい終了日</param>
    /// <remarks>実施日は動かさない。期間の外に出たときは、警告を出す。</remarks>
    partial void OnEndDateChanged(DateTimeOffset? value)
    {
        if (!_syncingDates)
        {
            AdjustInfo.Clear();
            if (value is { } end && StartDate is { } start && end.Date < start.Date)
            {
                _syncingDates = true;
                StartDate = end;
                _syncingDates = false;
            }
        }
        UpdatePeriodError();
    }

    /// <summary>実施日を変えたら、期間の外のときは、開始日・終了日を動かして合わせる</summary>
    /// <param name="oldValue">変える前の実施日</param>
    /// <param name="newValue">新しい実施日</param>
    /// <remarks>
    /// 開始日より前なら開始日を早め、終了日より後なら終了日を伸ばす。終了日が空 (無期限)のときは、終了日は動かさない。動かしたときは、知らせる。
    /// ほかの実施日にある日にしようとしたときは、先に入っていた日が勝つので、実施日を元の日に戻して、重複を知らせる。
    /// </remarks>
    partial void OnEventDateChanged(DateTimeOffset? oldValue, DateTimeOffset? newValue)
    {
        if (!_syncingDates)
        {
            AdjustInfo.Clear();
            if (newValue is { } eventDate)
            {
                if (OtherEventDates.Any(item => item.Date == DateOnly.FromDateTime(eventDate.Date)))
                {
                    _syncingDates = true;
                    try
                    {
                        EventDate = oldValue;
                    }
                    finally
                    {
                        _syncingDates = false;
                    }
                    AdjustInfo.Show("実施日が重複しています");
                    UpdatePeriodError();
                    return;
                }
                AdjustPeriodToEvent(eventDate);
            }
        }
        UpdatePeriodError();
    }

    /// <summary>ほかの実施日を、一覧に足す</summary>
    /// <param name="day">足す日</param>
    /// <remarks>
    /// 実施日と同じ日と、一覧にある日は、先に入っていた日が勝つので、足さない。期間の外の日は、開始日・終了日を動かして合わせて、知らせる。
    /// 一覧は、日付の順に並べる。
    /// </remarks>
    public void AddOtherEventDate(DateOnly day)
    {
        if ((EventDate is { } eventDate && DateOnly.FromDateTime(eventDate.Date) == day) || OtherEventDates.Any(item => item.Date == day))
        {
            return;
        }
        if (OtherEventDates.Count >= NoteLimits.OtherEventDatesMaxCount)
        {
            AdjustInfo.Show($"ほかの実施日は {NoteLimits.OtherEventDatesMaxCount} 日までです");
            return;
        }

        var index = 0;
        while (index < OtherEventDates.Count && OtherEventDates[index].Date < day)
        {
            index++;
        }
        OtherEventDates.Insert(index, new OtherEventDateItem(day, RemoveOtherEventDate));
        IsOtherEventDatesEmpty = false;

        AdjustInfo.Clear();
        AdjustPeriodToEvent(ToOffset(day));
        UpdatePeriodError();
    }

    /// <summary>ほかの実施日を、一覧から外す</summary>
    /// <param name="day">外す日</param>
    public void RemoveOtherEventDate(DateOnly day)
    {
        if (OtherEventDates.FirstOrDefault(item => item.Date == day) is { } item)
        {
            RemoveOtherEventDate(item);
        }
    }

    /// <summary>ほかの実施日の 1 行を、一覧から外す</summary>
    /// <param name="item">外す行</param>
    private void RemoveOtherEventDate(OtherEventDateItem item)
    {
        OtherEventDates.Remove(item);
        IsOtherEventDatesEmpty = OtherEventDates.Count == 0;
        UpdatePeriodError();
    }

    /// <summary>ほかの実施日の一覧を、渡された日付で置き換える (読み込むとき)</summary>
    /// <param name="days">日付</param>
    private void SetOtherEventDates(IEnumerable<DateOnly> days)
    {
        OtherEventDates.Clear();
        foreach (var day in days.Order())
        {
            OtherEventDates.Add(new OtherEventDateItem(day, RemoveOtherEventDate));
        }
        IsOtherEventDatesEmpty = OtherEventDates.Count == 0;
    }

    /// <summary>実施日が期間の外のとき、開始日・終了日を動かして、実施日を含める</summary>
    /// <param name="eventDate">実施日か、ほかの実施日</param>
    private void AdjustPeriodToEvent(DateTimeOffset eventDate)
    {
        var messages = new List<string>();
        _syncingDates = true;
        try
        {
            if (StartDate is { } start && eventDate.Date < start.Date)
            {
                StartDate = eventDate;
                messages.Add($"開始日を {FormatDate(eventDate)} に早めました。");
            }
            if (EndDate is { } end && eventDate.Date > end.Date)
            {
                EndDate = eventDate;
                messages.Add($"終了日を {FormatDate(eventDate)} に伸ばしました。");
            }
        }
        finally
        {
            _syncingDates = false;
        }

        if (messages.Count > 0)
        {
            AdjustInfo.Show(string.Concat(messages));
        }
    }

    /// <summary>実施日とほかの実施日が期間の外にあるかを調べて、警告と、外れた行の印と、保存できるかを合わせる</summary>
    /// <remarks>警告の文は、実施日が外れているときは実施日を、外れていなければ、ほかの実施日のいちばん早い外れた日を言う。</remarks>
    private void UpdatePeriodError()
    {
        IsEventDateInvalid = EventDate is { } eventDate && IsOutOfPeriod(eventDate);
        foreach (var item in OtherEventDates)
        {
            item.IsInvalid = StartDate is not null && IsOutOfPeriod(ToOffset(item.Date));
        }

        if (IsEventDateInvalid && EventDate is { } outEvent)
        {
            PeriodErrorMessage = $"実施日 ({FormatDate(outEvent)})が期間の外です。開始日・終了日か実施日を直してください。";
        }
        else if (OtherEventDates.FirstOrDefault(item => item.IsInvalid) is { } outOther)
        {
            PeriodErrorMessage = $"ほかの実施日 ({outOther.Date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)})が期間の外です。開始日・終了日かほかの実施日を直してください。";
        }
        else
        {
            PeriodErrorMessage = "";
        }
        IsPeriodErrorOpen = PeriodErrorMessage.Length > 0;
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>日付が、開始日から終了日の外にあるか (終了日が空は、終了日の側は常に中)</summary>
    /// <param name="date">調べる日</param>
    /// <returns>外なら true。開始日が空のときは false</returns>
    private bool IsOutOfPeriod(DateTimeOffset date)
        => StartDate is { } start && (date.Date < start.Date || (EndDate is { } end && date.Date > end.Date));

    /// <summary>終了日をクリアする (無期限にする)</summary>
    [RelayCommand]
    private void ClearEndDate() => EndDate = null;

    /// <summary>実施日をクリアする (未設定にする)</summary>
    [RelayCommand]
    private void ClearEventDate() => EventDate = null;

    /// <summary>保存して閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>失敗は、閉じずに書いた内容を残して、画面に出す。タイトルが空の間と、実施日が期間の外にある間は、押せない。</remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (StartDate is not { } start)
        {
            SaveError.Show("開始日を入力してください。");
            return;
        }

        var input = new NoteInput
        {
            Title = Title,
            Body = Body,
            Priority = (NotePriority)(PriorityIndex + PriorityOffset),
            IsPrivate = IsShared && IsPrivate,
            StartDate = DateOnly.FromDateTime(start.Date),
            EndDate = EndDate is { } end ? DateOnly.FromDateTime(end.Date) : DateOnly.MaxValue,
            EventDate = EventDate is { } eventDate ? DateOnly.FromDateTime(eventDate.Date) : DateOnly.MaxValue,
            OtherEventDates = [.. OtherEventDates.Select(item => item.Date)],
        };
        if (input.Validate() is { } error)
        {
            SaveError.Show(error);
            return;
        }

        try
        {
            var saved = _target is null
                ? await notes.AddAsync(input) is not null
                : await notes.UpdateAsync(_target.Id, input);
            if (!saved)
            {
                SaveError.Show("ノートを保存できませんでした。ほかの人が変更したか、見えなくなった可能性があります。");
                return;
            }
        }
        catch (Exception ex) when (ex is DataFileException or ArgumentException)
        {
            SaveError.Show(ex.Message);
            return;
        }

        CloseRequested?.Invoke(this, true);
    }

    /// <summary>キャンセルして閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    /// <summary>保存できる状態か。タイトルが空の間と、実施日が期間の外にある間は、保存できない。Web 版と同じ</summary>
    private bool CanSave => !IsPeriodErrorOpen && !string.IsNullOrWhiteSpace(Title);

    /// <summary>日付を、知らせの文に入れる形 (<c>yyyy/MM/dd</c>)にする</summary>
    /// <param name="date">日付</param>
    /// <returns>文字</returns>
    private static string FormatDate(DateTimeOffset date) => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    /// <summary>日付を、日付の欄に渡す形にする</summary>
    /// <param name="date">日付</param>
    /// <returns>その日の 0 時 (この PC の時差)</returns>
    private static DateTimeOffset ToOffset(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));
}
