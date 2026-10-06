using MmmTool.Data.Connection;

namespace MmmTool.Features.Database.Settings;

/// <summary>
/// 設定ページで選ぶ、保存先の種類 (選択肢)。
/// </summary>
/// <param name="Value">保存先の種類</param>
/// <param name="Name">画面に出す名前</param>
public sealed record DatabaseModeOption(DatabaseMode Value, string Name);
