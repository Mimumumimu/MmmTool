using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Core.Services;

namespace MmmTool.ViewModels;

/// <summary>設定ページの ViewModel</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>リマインダーの設定</summary>
    private readonly ReminderSettingsService _reminderSettings;

    /// <summary>ページのタイトル</summary>
    public string Title => "設定";

    /// <summary>スヌーズ間隔の下限（分）</summary>
    public double SnoozeIntervalMin => ReminderMonitor.MinSnoozeInterval;

    /// <summary>スヌーズ間隔の上限（分）</summary>
    public double SnoozeIntervalMax => ReminderMonitor.MaxSnoozeInterval;

    /// <summary>リマインダーのスヌーズ再通知間隔（分）</summary>
    /// <remarks>変わったら即保存する。範囲外・空は下限・上限に収めて画面にも反映する。</remarks>
    [ObservableProperty]
    public partial double SnoozeIntervalMinutes { get; set; }

    /// <summary>保存済みの値を読み込んで表示する</summary>
    public SettingsViewModel(ReminderSettingsService reminderSettings)
    {
        _reminderSettings = reminderSettings;
        SnoozeIntervalMinutes = reminderSettings.SnoozeIntervalMinutes;
    }

    /// <summary>値が変わったら範囲に収めて保存する</summary>
    partial void OnSnoozeIntervalMinutesChanged(double value)
    {
        // NumberBox は空にすると NaN になる
        var minutes = double.IsNaN(value) ? ReminderMonitor.DefaultSnoozeInterval : (int)Math.Round(value);
        var clamped = Math.Clamp(minutes, ReminderMonitor.MinSnoozeInterval, ReminderMonitor.MaxSnoozeInterval);
        if (clamped != value)
        {
            SnoozeIntervalMinutes = clamped;
            return;
        }
        _ = SaveSnoozeIntervalAsync(clamped);
    }

    /// <summary>スヌーズ間隔を保存する</summary>
    /// <remarks>失敗は握りつぶさない（async void と同じく未処理例外として扱う）。</remarks>
    private async Task SaveSnoozeIntervalAsync(int minutes)
    {
        await _reminderSettings.SetSnoozeIntervalMinutesAsync(minutes);
    }
}
