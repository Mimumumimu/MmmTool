using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Core.Reminders;

namespace MmmTool.Features.Settings;

/// <summary>設定ページの ViewModel</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>リマインダーの設定</summary>
    private readonly ReminderSettingsService _reminderSettings;

    /// <summary>ページのタイトル</summary>
    public string Title => "設定";

    /// <summary>スヌーズ間隔の下限（分）</summary>
    public double SnoozeIntervalMin => ReminderSettingsService.MinSnoozeInterval;

    /// <summary>スヌーズ間隔の上限（分）</summary>
    public double SnoozeIntervalMax => ReminderSettingsService.MaxSnoozeInterval;

    /// <summary>リマインダーのスヌーズ再通知間隔（分）</summary>
    /// <remarks>変わったら即保存する。範囲外・空は下限・上限に収めて画面にも反映する。</remarks>
    [ObservableProperty]
    public partial double SnoozeIntervalMinutes { get; set; }

    /// <summary>保存済みの値を読み込んで表示する</summary>
    /// <param name="reminderSettings">リマインダーの設定</param>
    public SettingsViewModel(ReminderSettingsService reminderSettings)
    {
        _reminderSettings = reminderSettings;
        SnoozeIntervalMinutes = reminderSettings.SnoozeIntervalMinutes;
    }

    /// <summary>値が変わったら範囲に収めて保存する</summary>
    /// <param name="value">変更後のスヌーズ間隔（分）</param>
    partial void OnSnoozeIntervalMinutesChanged(double value)
    {
        // NumberBox は空にすると NaN になる
        var minutes = double.IsNaN(value) ? ReminderSettingsService.DefaultSnoozeInterval : (int)Math.Round(value);
        var clamped = ReminderSettingsService.ClampSnoozeInterval(minutes);
        if (clamped != value)
        {
            SnoozeIntervalMinutes = clamped;
            return;
        }
        _ = SaveSnoozeIntervalAsync(clamped);
    }

    /// <summary>スヌーズ間隔を保存する</summary>
    /// <param name="minutes">保存するスヌーズ間隔（分）</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>失敗は握りつぶさない（async void と同じく未処理例外として扱う）。</remarks>
    private async Task SaveSnoozeIntervalAsync(int minutes)
    {
        await _reminderSettings.SetSnoozeIntervalMinutesAsync(minutes);
    }
}
