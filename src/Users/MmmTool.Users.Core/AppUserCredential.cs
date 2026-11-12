namespace MmmTool.Users.Core;

/// <summary>ログインの照合に使うユーザーとパスワードのハッシュ</summary>
/// <param name="User">ユーザー</param>
/// <param name="PasswordHash">
/// パスワードのハッシュで、<see cref="PasswordHasher"/> が作った形。
/// 空文字は新しく決める状態。
/// </param>
public sealed record AppUserCredential(AppUser User, string PasswordHash);
