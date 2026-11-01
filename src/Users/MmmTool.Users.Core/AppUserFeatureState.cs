namespace MmmTool.Users.Core;

/// <summary>
/// ユーザーと、ある機能を管理者に無効にされているか。
/// </summary>
/// <param name="User">ユーザー</param>
/// <param name="IsFeatureDisabled">指定した機能を、管理者に無効にされているとき true。設定が無い人は有効なので false</param>
public sealed record AppUserFeatureState(AppUser User, bool IsFeatureDisabled);
