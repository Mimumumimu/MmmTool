namespace MmmTool.WorkItems.Main;

/// <summary>状態と進捗度・実績の食い違いの警告</summary>
/// <param name="Row">入力した行</param>
/// <param name="ColumnKey">入力した列のキー (吹き出しを出すセル)</param>
/// <param name="Message">警告の文</param>
/// <remarks>入力したセルの下に短く出す</remarks>
public sealed record WorkWarning(WorkItemRow Row, string ColumnKey, string Message);
