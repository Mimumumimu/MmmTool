using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MmmTool.Notes.Edit;

/// <summary>
/// 入力ウィンドウの「ほかの実施日」の一覧の 1 行。日付と、削除の操作を持つ。
/// </summary>
/// <param name="date">日付</param>
/// <param name="remove">この行を一覧から外す処理 (「✕」ボタンが呼ぶ)</param>
public sealed partial class OtherEventDateItem(DateOnly date, Action<OtherEventDateItem> remove) : ObservableObject
{
    /// <summary>日付の欄と同じ、曜日つきの表示の形。PC の言語に依らず日本語</summary>
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    /// <summary>日付</summary>
    public DateOnly Date { get; } = date;

    /// <summary>一覧に出す文字で、<c>2026/11/17 (火)</c> の形</summary>
    public string Text { get; } = date.ToString("yyyy/MM/dd (ddd)", Japanese);

    /// <summary>期間の外にあるか。出ている間は、行の文字をエラーの色にして、保存できなくする</summary>
    [ObservableProperty]
    public partial bool IsInvalid { get; set; }

    /// <summary>この行を削除する</summary>
    [RelayCommand]
    private void Remove() => remove(this);
}
