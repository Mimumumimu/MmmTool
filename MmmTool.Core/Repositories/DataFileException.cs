namespace MmmTool.Core.Repositories;

/// <summary>
/// 保存データの読み書きに失敗した。メッセージはそのままユーザーへ表示できる形にする。
/// </summary>
public sealed class DataFileException(string message, Exception innerException) : Exception(message, innerException);
