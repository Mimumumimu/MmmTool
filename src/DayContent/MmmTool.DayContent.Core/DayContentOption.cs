namespace MmmTool.DayContent.Core;

/// <summary>
/// 内容の、真偽の選択肢 1 つ。
/// </summary>
/// <param name="Key">選択肢の識別 (この PC の設定のキーにも使う)</param>
/// <param name="Label">画面に出す名前</param>
/// <param name="Default">既定値</param>
public sealed record DayContentOption(string Key, string Label, bool Default);
