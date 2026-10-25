using MmmTool.Users.Core;

namespace MmmToolAdmin.Users;

/// <summary>ユーザーの一覧の 1 行</summary>
/// <param name="Id">ユーザーの番号</param>
/// <param name="DisplayName">表示名</param>
/// <param name="LoginName">ログイン名</param>
/// <param name="IsActive">今日使える状態か</param>
/// <param name="Status">状態の文字列</param>
public sealed record UserItem(int Id, string DisplayName, string LoginName, bool IsActive, string Status)
{
    /// <summary>ユーザーから、一覧の行を作る</summary>
    /// <param name="user">ユーザー</param>
    /// <param name="today">今日の日付</param>
    /// <returns>一覧の行</returns>
    public static UserItem From(AppUser user, DateOnly today)
    {
        var active = user.IsActiveOn(today);
        var status = active ? "使える" : user.IsDeleted ? "削除済み" : "期間外";
        return new UserItem(user.Id, user.DisplayName, user.LoginName, active, status);
    }
}
