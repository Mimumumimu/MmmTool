namespace MmmTool.Users.Core;

/// <summary>
/// ログインの照合に使う、ユーザーとパスワードのハッシュ。
/// </summary>
/// <param name="User">ユーザー</param>
/// <param name="PasswordHash">パスワードのハッシュ (<see cref="PasswordHasher"/>が作った形)。空文字は、新しく決める状態</param>
public sealed record AppUserCredential(AppUser User, string PasswordHash);
