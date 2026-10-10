using System.Globalization;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Main;

/// <summary>右のペインの、日ごとの記録の 1 行</summary>
/// <param name="record">日ごとの記録</param>
public sealed class WorkRecordItem(WorkRecord record)
{
    /// <summary>日付 (曜日つき)</summary>
    public string DateText { get; } = record.Date.ToString("yyyy/MM/dd (ddd)", CultureInfo.GetCultureInfo("ja-JP"));

    /// <summary>実績 (時間)。無ければ空</summary>
    public string HoursText { get; } = record.Hours > 0 ? $"{record.Hours:0.##} h" : "";

    /// <summary>日の備考 (全文)</summary>
    public string Note { get; } = record.Note;
}
