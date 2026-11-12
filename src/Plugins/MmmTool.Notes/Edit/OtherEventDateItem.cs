using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MmmTool.Notes.Edit;

/// <summary>入力ウィンドウの「ほかの実施日」の一覧の 1 行</summary>
/// <param name="date">日付</param>
/// <param name="remove">この行を一覧から外す処理 (「✕」ボタンが呼ぶ)</param>
/// <remarks>日付と削除の操作を持つ</remarks>
public sealed partial class OtherEventDateItem(DateOnly date, Action<OtherEventDateItem> remove) : ObservableObject
{
    /// <summary>日付の欄と同じ曜日つきの表示の形</summary>
    /// <remarks>PC の言語に依らず日本語</remarks>
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    /// <summary>日付</summary>
    public DateOnly Date { get; } = date;

    /// <summary>一覧に出す文字で、2026/11/17 の形</summary>
    public string Text { get; } = date.ToString("yyyy/MM/dd (ddd)", Japanese);

    /// <summary>期間の外にあるか</summary>
    /// <remarks>出ている間は行の文字をエラーの色にして、保存できなくする。</remarks>
    [ObservableProperty]
    public partial bool IsInvalid { get; set; }

    /// <summary>この行を削除する</summary>
    [RelayCommand]
    private void Remove() => remove(this);
}
