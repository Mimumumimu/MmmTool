using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Core.Reminders;

namespace MmmTool.Features.Reminders.Settings;

/// <summary>リマインダーの設定 (設定ページの部品)の ViewModel</summary>
public sealed partial class ReminderSettingsViewModel : ObservableObject
{
    /// <summary>リマインダーの設定</summary>
    private readonly ReminderSettingsService _reminderSettings;

    /// <summary>保存済みの値を入れている最中か</summary>
    /// <remarks>true の間は、値の変更で保存しない (読み込んだだけの値で、設定ファイルを書き換えないため)。</remarks>
    private bool _isInitializing;

    /// <summary>画面に出すエラー (保存に失敗したとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>設定を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>スヌーズ間隔の下限 (分)</summary>
    public double SnoozeIntervalMin => ReminderSettingsService.MinSnoozeInterval;

    /// <summary>スヌーズ間隔の上限 (分)</summary>
    public double SnoozeIntervalMax => ReminderSettingsService.MaxSnoozeInterval;

    /// <summary>リマインダーのスヌーズ再通知間隔 (分)</summary>
    /// <remarks>変わったら即保存する。範囲外・空は下限・上限に収めて画面にも反映する。</remarks>
    [ObservableProperty]
    public partial double SnoozeIntervalMinutes { get; set; }

    /// <summary>保存済みの値を読み込んで表示する</summary>
    /// <param name="reminderSettings">リマインダーの設定</param>
    public ReminderSettingsViewModel(ReminderSettingsService reminderSettings)
    {
        _reminderSettings = reminderSettings;
        _isInitializing = true;
        SnoozeIntervalMinutes = reminderSettings.SnoozeIntervalMinutes;
        _isInitializing = false;
        IsEditable = !reminderSettings.IsReadOnly;
    }

    /// <summary>値が変わったら範囲に収めて保存する</summary>
    /// <param name="value">変更後のスヌーズ間隔 (分)</param>
    partial void OnSnoozeIntervalMinutesChanged(double value)
    {
        if (_isInitializing)
        {
            return;
        }

        // NumberBox は空にすると NaN になる
        var minutes = double.IsNaN(value) ? ReminderSettingsService.DefaultSnoozeInterval : (int)Math.Round(value);
        var clamped = ReminderSettingsService.ClampSnoozeInterval(minutes);
        if (clamped != value)
        {
            SnoozeIntervalMinutes = clamped;
            return;
        }
        SaveSnoozeIntervalAsync(clamped).Forget();
    }

    /// <summary>スヌーズ間隔を保存する</summary>
    /// <param name="minutes">保存するスヌーズ間隔 (分)</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>
    /// 保存できない状態 (設定ファイルを読めなかった)では、入力欄を無効にしてあるので、ここへは来ない (来たら、保存されなかったことを知らせる)。
    /// 保存の失敗 (ロック・権限など)は、画面に出す。
    /// </remarks>
    private async Task SaveSnoozeIntervalAsync(int minutes)
    {
        try
        {
            if (await _reminderSettings.SetSnoozeIntervalMinutesAsync(minutes))
            {
                Error.Clear();
            }
            else
            {
                Error.Show("設定を読み込めなかったため、変更を保存できませんでした。");
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }
}
