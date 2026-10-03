using MmmSdk.Core.Repositories;

namespace MmmTool.Core.Services;

/// <summary>
/// リマインダー用の設定。汎用設定ストアにキー接頭辞付きで保存・取得する。
/// </summary>
/// <param name="settings">汎用設定ストア</param>
public sealed class ReminderSettingsService(ISettingsStore settings)
{
    /// <summary>スヌーズの再通知間隔（分）。保存値を範囲内に収めて返す（無ければ既定値）</summary>
    public int SnoozeIntervalMinutes => ClampSnoozeInterval(settings.Get(ReminderMonitor.SnoozeIntervalKey, ReminderMonitor.DefaultSnoozeInterval));

    /// <summary>スヌーズの再通知間隔（分）を保存する。範囲外は下限・上限に収める</summary>
    /// <param name="minutes">保存する間隔（分）</param>
    /// <returns>実際に保存した値</returns>
    public async Task<int> SetSnoozeIntervalMinutesAsync(int minutes)
    {
        var value = ClampSnoozeInterval(minutes);
        await settings.SetAsync(ReminderMonitor.SnoozeIntervalKey, value);
        return value;
    }

    /// <summary>スヌーズ間隔を下限〜上限に収める</summary>
    /// <param name="minutes">間隔（分）</param>
    /// <returns>下限〜上限に収めた間隔（分）</returns>
    private static int ClampSnoozeInterval(int minutes) =>
        Math.Clamp(minutes, ReminderMonitor.MinSnoozeInterval, ReminderMonitor.MaxSnoozeInterval);
}
