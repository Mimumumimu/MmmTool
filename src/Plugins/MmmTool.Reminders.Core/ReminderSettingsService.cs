using MmmSdk.Core.Components.Settings;

namespace MmmTool.Reminders.Core;

/// <summary>
/// リマインダー用の設定。汎用設定ストアにキー接頭辞付きで保存・取得する。
/// </summary>
/// <param name="settings">汎用設定ストア</param>
public sealed class ReminderSettingsService(ISettingsStore settings)
{
    /// <summary>スヌーズの再通知間隔 (分)の設定キー</summary>
    private const string SnoozeIntervalKey = "Reminder.SnoozeIntervalMinutes";

    /// <summary>スヌーズの再通知間隔 (分)の既定値</summary>
    public const int DefaultSnoozeInterval = 15;

    /// <summary>スヌーズの再通知間隔 (分)の最小値</summary>
    public const int MinSnoozeInterval = 5;

    /// <summary>スヌーズの再通知間隔 (分)の最大値</summary>
    public const int MaxSnoozeInterval = 999;

    /// <summary>スヌーズの再通知間隔 (分)。保存値を範囲内に収めて返す (無ければ既定値)</summary>
    public int SnoozeIntervalMinutes => ClampSnoozeInterval(settings.Get(SnoozeIntervalKey, DefaultSnoozeInterval));

    /// <summary>まとめて追加の「何分前に通知」の設定キー</summary>
    private const string BulkLeadKey = "Reminder.Bulk.LeadMinutes";

    /// <summary>まとめて追加の「通知を読み上げる」の設定キー</summary>
    private const string BulkSpeakKey = "Reminder.Bulk.IsSpeak";

    /// <summary>まとめて追加の「送信先」の設定キー</summary>
    private const string BulkChannelsKey = "Reminder.Bulk.ChannelIds";

    /// <summary>まとめて追加で、前回選んだ「何分前に通知」。保存値が選択肢に無ければ既定値</summary>
    public int BulkLeadMinutes
    {
        get
        {
            var minutes = settings.Get(BulkLeadKey, ReminderBulkLead.DefaultMinutes);
            return ReminderBulkLead.Minutes.Contains(minutes) ? minutes : ReminderBulkLead.DefaultMinutes;
        }
    }

    /// <summary>まとめて追加で、前回選んだ「通知を読み上げる」 (無ければ false)</summary>
    public bool BulkIsSpeak => settings.Get(BulkSpeakKey, false);

    /// <summary>まとめて追加で、前回選んだ送信先の番号 (無ければ空)</summary>
    /// <remarks>番号をカンマでつないだ文字列で持つ。例は <c>1,5</c> で、数に読めないものは捨てる。</remarks>
    public IReadOnlySet<int> BulkChannelIds
        => settings.Get(BulkChannelsKey, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();

    /// <summary>設定を保存できない状態か (設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている)</summary>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>設定ファイルを読めなかったときのメッセージ。正常なら null</summary>
    public string? LoadError => settings.LoadError;

    /// <summary>スヌーズの再通知間隔 (分)を保存する。範囲外は下限・上限に収める</summary>
    /// <param name="minutes">保存する間隔 (分)</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public Task<bool> SetSnoozeIntervalMinutesAsync(int minutes)
        => settings.SetAsync(SnoozeIntervalKey, ClampSnoozeInterval(minutes));

    /// <summary>まとめて追加で選んだ内容を、次に開いたときの初期値として保存する</summary>
    /// <param name="leadMinutes">何分前に通知するか</param>
    /// <param name="isSpeak">通知を読み上げるか</param>
    /// <param name="channelIds">選んだ送信先の番号</param>
    /// <returns>すべて保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public async Task<bool> SetBulkAsync(int leadMinutes, bool isSpeak, IEnumerable<int> channelIds)
    {
        var saved = await settings.SetAsync(BulkLeadKey, leadMinutes);
        saved &= await settings.SetAsync(BulkSpeakKey, isSpeak);
        saved &= await settings.SetAsync(BulkChannelsKey, string.Join(',', channelIds.Order()));
        return saved;
    }

    /// <summary>スヌーズ間隔を下限〜上限に収める</summary>
    /// <param name="minutes">間隔 (分)</param>
    /// <returns>下限〜上限に収めた間隔 (分)</returns>
    public static int ClampSnoozeInterval(int minutes) => Math.Clamp(minutes, MinSnoozeInterval, MaxSnoozeInterval);
}
