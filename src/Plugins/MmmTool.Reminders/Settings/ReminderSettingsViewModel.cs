using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Paths;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Speech;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>リマインダーの設定 (設定ページの部品)の ViewModel</summary>
public sealed partial class ReminderSettingsViewModel : ObservableObject
{
    /// <summary>リマインダーの設定</summary>
    private readonly ReminderSettingsService _reminderSettings;

    /// <summary>Windows の設定を開く処理</summary>
    private readonly IPathOpener _pathOpener;

    /// <summary>Windows の音声の設定 (声・速さ)を開くアドレス</summary>
    private const string SpeechSettingsUri = "ms-settings:speech";

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

    /// <summary>読み上げの音量の下限 (%)</summary>
    public double SpeechVolumeMin => 0;

    /// <summary>読み上げの音量の上限 (%)</summary>
    public double SpeechVolumeMax => 100;

    /// <summary>音量を変更できるか (設定ファイルを読めなかったときは、変更させない)</summary>
    public bool IsVolumeEditable => !_speech.IsVolumeReadOnly;

    /// <summary>読み上げの音量 (%)</summary>
    /// <remarks>スライダーを動かし終えるのを待ってから保存する (動かす間は、保存しない)。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeechVolumeText))]
    public partial double SpeechVolumePercent { get; set; }

    /// <summary>読み上げの音量の表示 (例: 50%)</summary>
    public string SpeechVolumeText => $"{SpeechVolumePercent:0}%";

    /// <summary>読み上げ (試し聞き)</summary>
    private readonly ISpeechService _speech;

    /// <summary>音量の保存を、スライダーが止まるまで待つ</summary>
    private readonly Debouncer _volumeSaveDebouncer = new(TimeSpan.FromMilliseconds(400));

    /// <summary>試し聞きの文</summary>
    private const string PreviewText = "読み上げの音量を確認します。";

    /// <summary>保存済みの値を読み込んで表示する</summary>
    /// <param name="reminderSettings">リマインダーの設定</param>
    /// <param name="pathOpener">Windows の設定を開く処理</param>
    /// <param name="speech">読み上げ</param>
    public ReminderSettingsViewModel(ReminderSettingsService reminderSettings, IPathOpener pathOpener, ISpeechService speech)
    {
        _reminderSettings = reminderSettings;
        _pathOpener = pathOpener;
        _speech = speech;
        _isInitializing = true;
        SnoozeIntervalMinutes = reminderSettings.SnoozeIntervalMinutes;
        SpeechVolumePercent = speech.VolumePercent;
        _isInitializing = false;
        IsEditable = !reminderSettings.IsReadOnly;
    }

    /// <summary>今の音量で、短い文を読み上げる</summary>
    /// <returns>読み上げの開始が済んだことを表すタスク</returns>
    /// <remarks>予約中の保存を待たず、いまのスライダーの値をすぐ保存してから読む (読み上げは保存値を使うため)。</remarks>
    [RelayCommand]
    private async Task PreviewSpeechAsync()
    {
        _volumeSaveDebouncer.Cancel();
        await SaveSpeechVolumeAsync((int)Math.Round(SpeechVolumePercent));
        await _speech.SpeakAsync(PreviewText);
    }

    /// <summary>値が変わったら範囲に収めて、少し待ってから保存する</summary>
    /// <param name="value">変更後の音量 (%)</param>
    partial void OnSpeechVolumePercentChanged(double value)
    {
        if (_isInitializing)
        {
            return;
        }

        // Slider は範囲内の値だけを出す。念のため、範囲外・NaN は収める
        var clamped = double.IsNaN(value) ? _speech.VolumePercent : (int)Math.Clamp(Math.Round(value), 0, 100);
        if (clamped != value)
        {
            SpeechVolumePercent = clamped;
            return;
        }
        _volumeSaveDebouncer.RunAsync(() => clamped, saved => SaveSpeechVolumeAsync(saved).Forget()).Forget();
    }

    /// <summary>音量を保存する</summary>
    /// <param name="percent">保存する音量 (%)</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存の失敗は、スヌーズ間隔と同じく画面に出す。</remarks>
    private async Task SaveSpeechVolumeAsync(int percent)
    {
        try
        {
            if (await _speech.SetVolumePercentAsync(percent))
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

    /// <summary>Windows の音声の設定を開く</summary>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>読み上げの声と速さは、Windows の音声の設定に従う。開けなかったとき (<see cref="PathOpenException"/>)は、画面に出す。</remarks>
    [RelayCommand]
    private async Task OpenSpeechSettingsAsync()
    {
        try
        {
            await _pathOpener.OpenAsync(SpeechSettingsUri);
        }
        catch (PathOpenException ex)
        {
            Error.Show(ex.Message);
        }
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
