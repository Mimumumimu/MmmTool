using MmmTool.Data.Connection;

namespace MmmTool.Features.Database.Connection;

/// <summary>
/// 接続の入力欄で選ぶ、ログインの方式 (選択肢)。
/// </summary>
/// <param name="Value">ログインの方式</param>
/// <param name="Name">画面に出す名前</param>
public sealed record DatabaseAuthenticationOption(DatabaseAuthentication Value, string Name);
