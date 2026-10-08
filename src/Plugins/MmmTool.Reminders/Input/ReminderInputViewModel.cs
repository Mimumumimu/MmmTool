using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Reminders.Input;

/// <summary>
/// リマインダー入力画面。リマインダー 1 件を新規登録・編集して保存する。
/// </summary>
/// <remarks>
/// 発動の指定は「日付を指定する」のオン (日付指定)とオフ (曜日指定)の 2 通り。曜日を 1 つも選ばない曜日指定は毎日通知になる。
/// 保存に成功したら <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class ReminderInputViewModel : ObservableObject
{
    /// <summary>件名が未入力のときのエラー</summary>
    private const string TitleRequiredMessage = "件名を入力してください。";

    /// <summary>リマインダーの読み書き</summary>
    private readonly ReminderService _reminders;
    /// <summary>ユーザーの保存先 (宛先の選択肢に使う)</summary>
    private readonly IAppUserRepository _users;
    /// <summary>送信先の保存先 (送信先の選択肢に使う)</summary>
    private readonly INotificationChannelRepository _channels;
    /// <summary>リマインダーの送信設定の保存先</summary>
    private readonly IReminderSendSettingRepository _sendSettings;
    /// <summary>現在日時</summary>
    private readonly TimeProvider _time;

    /// <summary>編集対象。新規なら null</summary>
    private Reminder? _target;

    /// <summary>読み込んだときに選んでいた送信先の番号 (なければ空)</summary>
    /// <remarks>ほかの人が登録した送信先も含む。保存のときに、選び直していなければ、選んだ内容を、そのまま書き直す (外さない)。</remarks>
    private HashSet<int> _initialChannelIds = [];

    /// <summary>ViewModel を作る</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="users">ユーザーの保存先</param>
    /// <param name="channels">送信先の保存先</param>
    /// <param name="sendSettings">リマインダーの送信設定の保存先</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderInputViewModel(
        ReminderService reminders, IAppUserRepository users, INotificationChannelRepository channels,
        IReminderSendSettingRepository sendSettings, TimeProvider time)
    {
        _reminders = reminders;
        _users = users;
        _channels = channels;
        _sendSettings = sendSettings;
        _time = time;
        WeekdayOptions = [.. ReminderDates.WeekdayNames.Select(weekday => new WeekdayOption(weekday.Flag, weekday.Name))];
        Load(null);
    }

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>保存したときは保存した内容、キャンセルのときは null を渡す。</remarks>
    public event EventHandler<Reminder?>? CloseRequested;

    /// <summary>画面のタイトル</summary>
    /// <remarks>新規は「リマインダー入力」、編集 (採番済み)は「リマインダー編集」。</remarks>
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

    /// <summary>日付を指定するか (オフなら曜日指定)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekdaySpecified))]
    public partial bool IsDateSpecified { get; set; }

    /// <summary>曜日指定か (<see cref="IsDateSpecified"/> の逆)</summary>
    public bool IsWeekdaySpecified => !IsDateSpecified;

    /// <summary>発動日 (日付指定のとき)</summary>
    [ObservableProperty]
    public partial DateTimeOffset? Date { get; set; }

    /// <summary>曜日の選択肢 (月〜日)</summary>
    public IReadOnlyList<WeekdayOption> WeekdayOptions { get; }

    /// <summary>発動時刻の時 (0〜23)</summary>
    [ObservableProperty]
    public partial int Hour { get; set; }

    /// <summary>発動時刻の分 (0〜59)</summary>
    [ObservableProperty]
    public partial int Minute { get; set; }

    /// <summary>備考</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = "";

    /// <summary>リンク (URL・ファイル・フォルダのパス)</summary>
    [ObservableProperty]
    public partial string Link { get; set; } = "";

    /// <summary>通知のときに読み上げるか</summary>
    [ObservableProperty]
    public partial bool IsSpeak { get; set; }

    /// <summary>宛先を選ぶ欄を出すか (DB モードで、今のユーザーを特定できているときだけ)</summary>
    public bool IsTargetVisible => _reminders.CurrentUserId != 0;

    /// <summary>宛先の選択肢 (自分・全員・ほかの使えるユーザー)</summary>
    public ObservableCollection<ReminderTargetOption> TargetOptions { get; } = [];

    /// <summary>選んでいる宛先</summary>
    [ObservableProperty]
    public partial ReminderTargetOption? SelectedTarget { get; set; }

    /// <summary>送信先を選ぶ欄を出すか (DB モードで、今のユーザーを特定できていて、送信先を読み込めたときだけ)</summary>
    [ObservableProperty]
    public partial bool IsChannelVisible { get; private set; }

    /// <summary>送信先の選択肢 (自分が登録した送信先と、保存されている送信先。チェックで、複数選べる)</summary>
    public ObservableCollection<ReminderChannelOption> ChannelOptions { get; } = [];

    /// <summary>送信先が 1 つも無いか (送信先の欄を出していて、選べるものが無い)</summary>
    [ObservableProperty]
    public partial bool HasNoChannels { get; private set; }

    /// <summary>保存のエラー</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>送信先の選択肢を作り、編集なら、保存されている送信先にチェックを入れる</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// <see cref="Load"/> のあとに呼ぶ。送信先を使えないとき (ローカルモード・ユーザー未特定)は何もしない。
    /// 編集で、保存されている送信先がほかの人の登録のときも、保存で外れないよう、選択肢に足す。
    /// 読み込めなかったとき (接続できない・表が無い)は、エラーを出して、送信先の欄を出さない。
    /// </remarks>
    public async Task LoadChannelsAsync()
    {
        ChannelOptions.Clear();
        _initialChannelIds = [];
        HasNoChannels = false;
        IsChannelVisible = _channels.IsAvailable && _sendSettings.IsAvailable && _reminders.CurrentUserId != 0;
        if (!IsChannelVisible)
        {
            return;
        }

        try
        {
            var mine = await _channels.GetChannelsAsync(includeDeleted: false);
            var settings = await _sendSettings.GetChannelIdsAsync();
            if (_target is { No: > 0 } target && settings.TryGetValue(target.No, out var channelIds))
            {
                _initialChannelIds = [.. channelIds];
            }

            foreach (var channel in mine)
            {
                ChannelOptions.Add(new ReminderChannelOption(channel.Id, channel.Kind, channel.Name, _initialChannelIds.Contains(channel.Id)));
            }

            // 保存されている送信先がほかの人の登録のときも、保存で外れないよう、選択肢に足す (チェックを入れたまま)
            foreach (var channelId in _initialChannelIds.Where(id => ChannelOptions.All(option => option.Id != id)))
            {
                var other = await _channels.FindAsync(channelId);
                ChannelOptions.Add(new ReminderChannelOption(channelId, other?.Kind, other?.Name ?? $"送信先 {channelId}", isChecked: true));
            }
            HasNoChannels = ChannelOptions.Count == 0;
        }
        catch (DataFileException ex)
        {
            ChannelOptions.Clear();
            IsChannelVisible = false;
            SaveError.Show(ex.Message);
        }
    }

    /// <summary>宛先の選択肢を作り、新規なら「自分」・編集なら保存されている宛先を選ぶ</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// <see cref="Load"/> のあとに呼ぶ。宛先を出さないとき (<see cref="IsTargetVisible"/> が false)は何もしない。
    /// ユーザーの一覧を読めなかったときは、エラーを出して、「自分」と「全員」だけにする。
    /// </remarks>
    public async Task LoadTargetsAsync()
    {
        TargetOptions.Clear();
        if (!IsTargetVisible)
        {
            SelectedTarget = null;
            return;
        }

        var selfId = _reminders.CurrentUserId;
        TargetOptions.Add(new ReminderTargetOption(selfId, "自分"));
        TargetOptions.Add(new ReminderTargetOption(0, "全員"));

        IReadOnlyList<AppUser> users = [];
        try
        {
            users = await _users.GetUsersAsync();
        }
        catch (DataFileException ex)
        {
            SaveError.Show(ex.Message);
        }

        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        foreach (var user in users.Where(user => user.Id != selfId && user.IsActiveOn(today)).OrderBy(user => user.DisplayName, StringComparer.CurrentCulture))
        {
            TargetOptions.Add(new ReminderTargetOption(user.Id, user.DisplayName));
        }

        // 編集で、宛先がもう選べない (削除済み・期間外のユーザー)ときも、保存で宛先が変わらないよう、選択肢に足す
        var targetId = _target?.TargetUserId ?? selfId;
        if (TargetOptions.All(option => option.UserId != targetId))
        {
            var name = users.FirstOrDefault(user => user.Id == targetId)?.DisplayName ?? $"ユーザー {targetId}";
            TargetOptions.Add(new ReminderTargetOption(targetId, name));
        }
        SelectedTarget = TargetOptions.First(option => option.UserId == targetId);
    }

    /// <summary>入力欄に読み込む</summary>
    /// <param name="target">編集するリマインダー。新規なら null</param>
    /// <remarks>新規は 日付＝今日・時刻＝現在時刻・曜日指定 (曜日は未選択)・ほかは空。編集は対象の値を読み込む (曜日指定なら日付欄は今日)。</remarks>
    public void Load(Reminder? target)
    {
        _target = target;
        var now = _time.GetLocalNow();
        var today = new DateTimeOffset(now.Date, now.Offset);

        WindowTitle = target is { No: > 0 } ? "リマインダー編集" : "リマインダー入力";
        Title = target?.Title ?? "";
        Note = target?.Note ?? "";
        Link = target?.Link ?? "";
        IsSpeak = target?.IsSpeak ?? false;

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

        // 読み込みで件名が変わってもエラーは出さない (エラーは保存しようとしたときだけ)
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
            IsSpeak = IsSpeak,
            TargetUserId = SelectedTarget?.UserId ?? _target?.TargetUserId ?? 0,
        };

        try
        {
            var saved = await _reminders.SaveAsync(reminder);
            // 送信設定の保存に失敗して画面が開いたままのとき、もう一度保存しても、新しいリマインダーを重ねて作らないよう、保存した内容を編集対象にする
            _target = saved;
            // 送信先を選んでいるときは、保存のたびに、送信設定を書き直して、更新日時を今に進める。
            // MmmBatch は、更新日時が今日の発動時刻より後なら、今日の分は送らない。保存した時点で、送る時刻がもう過去なら今日は送らず、まだ先なら、その時刻に送る
            var selected = ChannelOptions.Where(option => option.IsChecked).Select(option => option.Id).ToHashSet();
            if (IsChannelVisible && (selected.Count > 0 || !_initialChannelIds.SetEquals(selected)))
            {
                await _sendSettings.SetChannelsAsync(saved.No, selected);
                _initialChannelIds = selected;
            }
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
