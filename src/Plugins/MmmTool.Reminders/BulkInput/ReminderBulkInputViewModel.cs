using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.BulkInput;

/// <summary>
/// まとめて追加の画面。時刻の表で会議の時刻を選び、日付指定のリマインダーを複数件まとめて登録する。
/// </summary>
/// <remarks>
/// 中の表 (0〜23 時の行に、:00 と :30 のマス)でマスを選ぶと、右の一覧に会議が 1 件ずつ増える。一覧では、時刻・件名・リンクを直せる。直さなければ、そのまま登録できる。
/// 一覧は選んだ順ではなく、マスを選んだ時点で、時刻の順になる位置に入る。時刻を直した行は、直している最中に動かないよう、その場に残す。
/// 左の列の日付・何分前・読み上げ・送信先は、全部の会議に同じ内容を付ける。日付・何分前・読み上げが変わるたびに、行の下の文を作り直す。
/// 同じ日付・時刻・件名のリマインダーがすでにあるときと、一覧に同じものが重なるときは、その行を追加しない。
/// 宛先は自分で、ローカルモードでは 0 にする。登録できたら <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class ReminderBulkInputViewModel : ObservableObject
{
    /// <summary>同じ予定があるときの、行の下の文</summary>
    private const string DuplicateMessage = "同じ予定があるため、追加しません。";

    /// <summary>鳴らす時刻がすでに過ぎているときの、行の下の文</summary>
    private const string PastMessage = "時刻が過ぎているため、すぐに通知します。";

    /// <summary>リマインダーの読み書き</summary>
    private readonly ReminderService _reminders;
    /// <summary>リマインダーの設定 (前回選んだ内容の保存と読み込み)</summary>
    private readonly ReminderSettingsService _settings;
    /// <summary>送信先の選択肢を作る</summary>
    private readonly ReminderChannelLoader _channelLoader;
    /// <summary>リマインダーの送信設定の保存先</summary>
    private readonly IReminderSendSettingRepository _sendSettings;
    /// <summary>現在日時</summary>
    private readonly TimeProvider _time;

    /// <summary>すでにあるリマインダーの日付・時刻・件名 (論理削除済みを除く)</summary>
    private HashSet<(int Date, int Time, string Title)> _existing = [];

    /// <summary>追加するリマインダー (一覧のうち、重ならない行)</summary>
    private List<Reminder> _toAdd = [];

    /// <summary>読み込みを終えたか (初期値を入れている間は、選びを保存しない)</summary>
    private bool _isLoaded;

    /// <summary>ViewModel を作る</summary>
    /// <param name="reminders">リマインダーの読み書き</param>
    /// <param name="settings">リマインダーの設定</param>
    /// <param name="channelLoader">送信先の選択肢を作る</param>
    /// <param name="sendSettings">リマインダーの送信設定の保存先</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderBulkInputViewModel(
        ReminderService reminders, ReminderSettingsService settings, ReminderChannelLoader channelLoader,
        IReminderSendSettingRepository sendSettings, TimeProvider time)
    {
        _reminders = reminders;
        _settings = settings;
        _channelLoader = channelLoader;
        _sendSettings = sendSettings;
        _time = time;

        HourRows = [.. Enumerable.Range(0, 24).Select(hour => new ReminderBulkHourRow(
            hour, new ReminderBulkSlot(new TimeOnly(hour, 0)), new ReminderBulkSlot(new TimeOnly(hour, 30))))];
        foreach (var row in HourRows)
        {
            row.OnTheHour.PropertyChanged += OnSlotChanged;
            row.HalfPast.PropertyChanged += OnSlotChanged;
        }

        LeadOptions = [.. ReminderBulkLead.Minutes.Select(minutes => new ReminderBulkLeadOption(minutes, minutes == 0 ? "ちょうど" : $"{minutes} 分前"))];
        // 前回選んだ内容を初期値にする (毎回選び直す手間を省くため)
        SelectedLead = LeadOptions.First(option => option.Minutes == settings.BulkLeadMinutes);
        IsSpeak = settings.BulkIsSpeak;

        var now = time.GetLocalNow();
        Date = new DateTimeOffset(now.Date, now.Offset);

        // 今の時刻に一番近い 30 分刻みの時刻の、時の行。開いたときに、表をここまでスクロールする
        var nearestMinutes = (int)Math.Round((now.Hour * 60 + now.Minute) / 30.0) * 30;
        InitialHour = Math.Min(23, nearestMinutes / 60);
    }

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>登録した件数を渡す。キャンセルのときは 0。</remarks>
    public event EventHandler<int>? CloseRequested;

    /// <summary>時刻の表の行 (0〜23 時)</summary>
    public IReadOnlyList<ReminderBulkHourRow> HourRows { get; }

    /// <summary>開いたときに、表の上端に置く行の時</summary>
    /// <remarks>今の時刻に一番近い、30 分刻みの時刻の時。</remarks>
    public int InitialHour { get; }

    /// <summary>発動日</summary>
    [ObservableProperty]
    public partial DateTimeOffset? Date { get; set; }

    /// <summary>何分前かの選択肢</summary>
    public IReadOnlyList<ReminderBulkLeadOption> LeadOptions { get; }

    /// <summary>選んでいる、何分前か</summary>
    [ObservableProperty]
    public partial ReminderBulkLeadOption SelectedLead { get; set; }

    /// <summary>通知のときに読み上げるか</summary>
    [ObservableProperty]
    public partial bool IsSpeak { get; set; }

    /// <summary>送信先を選ぶ欄を出すか (DB モードで、今のユーザーを特定できていて、送信先を読み込めたときだけ)</summary>
    [ObservableProperty]
    public partial bool IsChannelVisible { get; private set; }

    /// <summary>自分が登録した送信先の選択肢</summary>
    /// <remarks>チェックで、複数選べる。</remarks>
    public ObservableCollection<ReminderChannelOption> ChannelOptions { get; } = [];

    /// <summary>送信先が 1 つも無いか (送信先の欄を出していて、選べるものが無い)</summary>
    [ObservableProperty]
    public partial bool HasNoChannels { get; private set; }

    /// <summary>選んだ会議の一覧</summary>
    public ObservableCollection<ReminderBulkEntry> Entries { get; } = [];

    /// <summary>一覧の見出し</summary>
    [ObservableProperty]
    public partial string ListHeader { get; private set; } = "選んだ会議";

    /// <summary>一覧が空か</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>登録のエラー</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>すでにあるリマインダーと、送信先を読み、行の下の文を作る</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// 読めなかったときは、エラーを出して、すでにあるものは無いものとして扱う。
    /// 送信先は、使えるとき (<see cref="ReminderChannelLoader.IsAvailable"/>)だけ読み、前回選んだ送信先にチェックを入れる。読めなかったときは、送信先の欄を出さない。
    /// </remarks>
    public async Task LoadAsync()
    {
        try
        {
            await LoadExistingAsync();
        }
        catch (DataFileException ex)
        {
            SaveError.Show(ex.Message);
        }

        IsChannelVisible = _channelLoader.IsAvailable;
        if (IsChannelVisible)
        {
            try
            {
                foreach (var option in await _channelLoader.LoadMineAsync(_settings.BulkChannelIds))
                {
                    option.PropertyChanged += OnChannelChanged;
                    ChannelOptions.Add(option);
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
        Refresh();
        _isLoaded = true;
    }

    /// <summary>送信先のチェックが変わったら、選びを覚える</summary>
    /// <param name="sender">変わった送信先の選択肢</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnChannelChanged(object? sender, PropertyChangedEventArgs e) => RememberChoices();

    /// <summary>読み上げの選びが変わったら、覚えて、追加するリマインダーを作り直す</summary>
    /// <param name="value">変更後の選び</param>
    partial void OnIsSpeakChanged(bool value)
    {
        Refresh();
        RememberChoices();
    }

    /// <summary>何分前かが変わったら、覚えて、行の下の文を作り直す</summary>
    /// <param name="value">変更後の選択</param>
    partial void OnSelectedLeadChanged(ReminderBulkLeadOption value)
    {
        Refresh();
        RememberChoices();
    }

    /// <summary>何分前・読み上げ・送信先の選びを、次に開いたときの初期値として保存する</summary>
    /// <remarks>
    /// 登録しないで閉じても残るよう、選びが変わるたびに保存する。読み込みの前 (初期値を入れている間)は保存しない。
    /// 送信先を出していないときは、保存してある送信先を、そのまま残す。
    /// 設定を保存できない状態 (<see cref="ReminderSettingsService.IsReadOnly"/>)でも、登録の邪魔はしない。
    /// </remarks>
    private void RememberChoices()
    {
        if (!_isLoaded)
        {
            return;
        }

        var channelIds = IsChannelVisible
            ? ChannelOptions.Where(option => option.IsChecked).Select(option => option.Id).ToHashSet()
            : _settings.BulkChannelIds;
        _settings.SetBulkAsync(SelectedLead.Minutes, IsSpeak, channelIds).Forget();
    }

    /// <summary>日付が変わったら、行の下の文を作り直す</summary>
    /// <param name="oldValue">変更前の日付</param>
    /// <param name="newValue">変更後の日付</param>
    /// <remarks>カレンダーで選択中の日付をもう一度押すと選択が外れるため、空になったら元の日付に戻す。</remarks>
    partial void OnDateChanged(DateTimeOffset? oldValue, DateTimeOffset? newValue)
    {
        if (newValue is null && oldValue is not null)
        {
            Date = oldValue;
            return;
        }
        Refresh();
    }

    /// <summary>表のマスの選びが変わったら、一覧に会議を足す・外す</summary>
    /// <param name="sender">選びが変わったマス</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnSlotChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ReminderBulkSlot slot || e.PropertyName != nameof(ReminderBulkSlot.IsChecked))
        {
            return;
        }

        if (slot.IsChecked)
        {
            var entry = new ReminderBulkEntry(slot);
            entry.PropertyChanged += OnEntryChanged;

            // 時刻の順になる位置へ入れる
            var index = Entries.Count;
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].StartTime > slot.Time)
                {
                    index = i;
                    break;
                }
            }
            Entries.Insert(index, entry);
        }
        else if (Entries.FirstOrDefault(entry => entry.Slot == slot) is { } removed)
        {
            removed.PropertyChanged -= OnEntryChanged;
            Entries.Remove(removed);
        }
        Refresh();
    }

    /// <summary>一覧の行の時刻・件名・リンクが変わったら、行の下の文を作り直す</summary>
    /// <param name="sender">変わった行</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 行の下の文を書き換えたことでは、作り直さない
        if (e.PropertyName is not (nameof(ReminderBulkEntry.Note) or nameof(ReminderBulkEntry.HasNote)))
        {
            Refresh();
        }
    }

    /// <summary>一覧の会議を、すべて登録して閉じる</summary>
    /// <returns>登録の完了を表すタスク</returns>
    /// <remarks>
    /// 登録の直前に、すでにあるリマインダーを読み直し、重なるものを除く。途中で失敗したときも、押し直せば、登録済みの分を重ねない。
    /// 時刻の順に登録する。失敗したらエラーを出して閉じない。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        SaveError.Clear();
        try
        {
            await LoadExistingAsync();
            var channelIds = ChannelOptions.Where(option => option.IsChecked).Select(option => option.Id).ToHashSet();
            var count = 0;
            foreach (var reminder in _toAdd.Where(reminder => !_existing.Contains(KeyOf(reminder))).OrderBy(reminder => reminder.Time))
            {
                var saved = await _reminders.SaveAsync(reminder);
                // 送信先を選んでいるときは、送信設定も書く (送る処理は MmmBatch が行う)
                if (IsChannelVisible && channelIds.Count > 0)
                {
                    await _sendSettings.SetChannelsAsync(saved.No, channelIds);
                }
                count++;
            }
            CloseRequested?.Invoke(this, count);
        }
        catch (DataFileException ex)
        {
            SaveError.Show(ex.Message);
            Refresh();
        }
    }

    /// <summary>登録できるか</summary>
    /// <returns>追加する会議があれば true</returns>
    private bool CanRegister() => _toAdd.Count > 0;

    /// <summary>保存せずに閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, 0);

    /// <summary>すでにあるリマインダーを読む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    private async Task LoadExistingAsync()
    {
        var reminders = await _reminders.GetRemindersAsync(includeDeleted: false);
        _existing = [.. reminders.Select(KeyOf)];
    }

    /// <summary>重なりを調べる鍵にする</summary>
    /// <param name="reminder">リマインダー</param>
    /// <returns>日付・時刻・件名</returns>
    private static (int Date, int Time, string Title) KeyOf(Reminder reminder) => (reminder.Date, reminder.Time, reminder.Title);

    /// <summary>追加するリマインダーと、一覧の行の下の文を作り直す</summary>
    private void Refresh()
    {
        var now = _time.GetLocalNow().DateTime;
        var nowTime = new TimeOnly(now.Hour, now.Minute);
        var date = DateOnly.FromDateTime((Date ?? new DateTimeOffset(now)).DateTime);
        var isToday = date == DateOnly.FromDateTime(now);
        var lead = SelectedLead.Minutes;

        _toAdd = [];
        HashSet<(int Date, int Time, string Title)> seen = [.. _existing];
        foreach (var entry in Entries)
        {
            var draft = new ReminderDraft(entry.StartTime, entry.Title, entry.Link);
            var reminder = draft.ToReminder(date, lead, _reminders.CurrentUserId, IsSpeak);
            var time = draft.Resolve(lead).Time;
            if (!seen.Add(KeyOf(reminder)))
            {
                entry.Note = DuplicateMessage;
                continue;
            }

            _toAdd.Add(reminder);
            entry.Note = isToday && time <= nowTime ? PastMessage : "";
        }

        IsEmpty = Entries.Count == 0;
        ListHeader = _toAdd.Count > 0 ? $"選んだ会議 ({_toAdd.Count} 件を追加)" : "選んだ会議";
        RegisterCommand.NotifyCanExecuteChanged();
    }
}
