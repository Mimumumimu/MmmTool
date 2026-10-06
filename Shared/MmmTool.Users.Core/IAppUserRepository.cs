namespace MmmTool.Users.Core;

/// <summary>
/// ユーザー (<see cref="AppUser"/>)の保存先。
/// </summary>
public interface IAppUserRepository
{
    /// <summary>ユーザーの一覧を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>全ユーザー (論理削除済み・使える期間の外も含む)。番号の順</returns>
    Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>ユーザーを登録する</summary>
    /// <param name="user">登録するユーザー (<see cref="AppUser.Id"/> は無視する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>登録した内容 (保存先が決めた番号つき)</returns>
    Task<AppUser> AddAsync(AppUser user, CancellationToken cancellationToken = default);
}
