using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.DaysOfWeek;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.Main;
using Windows.System;

namespace MmmTool.WorkItems.AutoEntry;

/// <summary>自動入力の時間と曜日を設定する入力枠</summary>
/// <remarks>
/// 「保存」を押したときと、時間の欄で Enter を押したときだけ、設定を渡す。渡すイベントは <see cref="SaveRequested"/>。
/// 入力が 2 つあり、途中で閉じたときに、中途半端な設定を保存しないため。
/// 外側を押したときと Esc は、入れ物のフライアウトが閉じるだけで、何も渡さない。
/// 時間が読めない・範囲外のときは、保存を押せない。時間を空か 0 にして保存すると、設定のクリアになる。
/// </remarks>
public sealed partial class AutoEntryEditor : UserControl
{
    /// <summary>入力枠を作る</summary>
    public AutoEntryEditor()
    {
        InitializeComponent();
    }

    /// <summary>「保存」が押された (引数は、入力された時間と曜日)</summary>
    public event EventHandler<AutoEntrySetting>? SaveRequested;

    /// <summary>「クリア」が押された</summary>
    public event EventHandler? ClearRequested;

    /// <summary>今の設定を入力欄に入れる</summary>
    /// <param name="hours">自動入力の時間 (0 は設定なし)</param>
    /// <param name="weekdays">自動入力する曜日</param>
    public void Load(double hours, Weekdays weekdays)
    {
        HoursBox.Text = hours > 0 ? WorkItemRow.FormatHours(hours) : "";
        WeekdayPicker.Value = weekdays;
        ClearButton.IsEnabled = hours > 0;
        ShowValidation();
    }

    /// <summary>時間の欄にフォーカスを移し、全体を選ぶ</summary>
    public void FocusHours()
    {
        HoursBox.Focus(FocusState.Programmatic);
        HoursBox.SelectAll();
    }

    /// <summary>時間の欄の文字が変わったら、読めるかを確かめて、保存を押せるかを決める</summary>
    /// <param name="sender">時間の欄</param>
    /// <param name="e">イベントの情報</param>
    private void OnHoursChanged(object sender, TextChangedEventArgs e) => ShowValidation();

    /// <summary>時間の欄で Enter が押されたら、保存する</summary>
    /// <param name="sender">時間の欄</param>
    /// <param name="e">キー入力の情報</param>
    private void OnHoursKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Save();
        }
    }

    /// <summary>「保存」が押されたら、保存する</summary>
    /// <param name="sender">ボタン</param>
    /// <param name="e">イベントの情報</param>
    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    /// <summary>「クリア」が押されたら、設定のクリアを渡す</summary>
    /// <param name="sender">ボタン</param>
    /// <param name="e">イベントの情報</param>
    private void OnClearClick(object sender, RoutedEventArgs e) => ClearRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>入力された時間と曜日を渡す (時間が読めないときは、何もしない)</summary>
    private void Save()
    {
        if (WorkItemViewModel.TryParseAutoHours(HoursBox.Text, out var hours))
        {
            SaveRequested?.Invoke(this, new AutoEntrySetting(hours, WeekdayPicker.Value));
        }
    }

    /// <summary>時間の入力が正しいかを確かめて、エラーの文と保存ボタンの状態に反映する</summary>
    private void ShowValidation()
    {
        var isValid = WorkItemViewModel.TryParseAutoHours(HoursBox.Text, out _);
        ErrorText.Text = isValid ? "" : $"時間は 0 以上 {WorkItemLimits.DailyHoursMax:0} 以下の数で入力してください。";
        ErrorText.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = isValid;
    }
}

/// <summary>入力枠で決めた、自動入力の設定</summary>
/// <param name="Hours">時間 (0 は設定のクリア)</param>
/// <param name="Weekdays">曜日</param>
public readonly record struct AutoEntrySetting(double Hours, Weekdays Weekdays);
