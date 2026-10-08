namespace MmmTool.Users.Core;

/// <summary>
/// ユーザー (<see cref="AppUser"/>)の保存先。
/// </summary>
public interface IAppUserRepository
{
    /// <summary>ユーザーの一覧を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>全ユーザー (論理削除済み・使える期間の外も含む)。番号の順。パスワードのハッシュは含まない</returns>
    Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>ログイン名から、ログインの照合に使う情報を取得する</summary>
    /// <param name="loginName">ログイン名 (大文字と小文字は区別しない)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除されていないユーザーとハッシュ。無ければ null</returns>
    Task<AppUserCredential?> FindByLoginNameAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>ユーザーを登録する</summary>
    /// <param name="user">登録するユーザー (<see cref="AppUser.Id"/> は無視する)</param>
    /// <param name="passwordHash">パスワードのハッシュ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>登録した内容 (保存先が決めた番号つき)</returns>
    /// <exception cref="LoginNameTakenException">同じログイン名が、削除されていないユーザーにある。</exception>
    Task<AppUser> AddAsync(AppUser user, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>パスワードが空のユーザーに、新しいパスワードのハッシュを入れる</summary>
    /// <param name="userId">ユーザーの番号</param>
    /// <param name="passwordHash">新しいパスワードのハッシュ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>入れたら true。すでにパスワードがある (ほかの人が先に決めた)ときは false</returns>
    Task<bool> SetPasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>ユーザーの表示名とログイン名を変える</summary>
    /// <param name="user">変えたあとの内容 (<see cref="AppUser.Id"/> のユーザーの、表示名とログイン名だけを書き換える)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更の完了を表すタスク</returns>
    /// <exception cref="LoginNameTakenException">同じログイン名が、ほかの削除されていないユーザーにある。</exception>
    Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default);

    /// <summary>ユーザーのパスワードのハッシュを入れ替える</summary>
    /// <param name="userId">ユーザーの番号</param>
    /// <param name="passwordHash">新しいパスワードのハッシュ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>入れ替えたら true。ユーザーが見つからない (削除された)ときは false</returns>
    /// <remarks>今のハッシュの確認は呼ぶ側が先に行う (<see cref="SetPasswordHashAsync"/> と違い、空でなくても書く)。</remarks>
    Task<bool> ChangePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);
}
