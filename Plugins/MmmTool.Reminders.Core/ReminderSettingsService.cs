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

    /// <summary>設定を保存できない状態か (設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている)</summary>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>設定ファイルを読めなかったときのメッセージ。正常なら null</summary>
    public string? LoadError => settings.LoadError;

    /// <summary>スヌーズの再通知間隔 (分)を保存する。範囲外は下限・上限に収める</summary>
    /// <param name="minutes">保存する間隔 (分)</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public Task<bool> SetSnoozeIntervalMinutesAsync(int minutes)
        => settings.SetAsync(SnoozeIntervalKey, ClampSnoozeInterval(minutes));

    /// <summary>スヌーズ間隔を下限〜上限に収める</summary>
    /// <param name="minutes">間隔 (分)</param>
    /// <returns>下限〜上限に収めた間隔 (分)</returns>
    public static int ClampSnoozeInterval(int minutes) => Math.Clamp(minutes, MinSnoozeInterval, MaxSnoozeInterval);
}
