namespace MmmTool.Users.Core;

/// <summary>
/// ログインしている人が、自分のアカウント (表示名・ログイン名・パスワード)を変える。ログアウトもここで行う。
/// </summary>
/// <param name="users">ユーザーの保存先</param>
/// <param name="saved">覚えているログイン名とパスワード</param>
/// <param name="currentUser">今のユーザー</param>
/// <remarks>
/// 変えたあとは、今のユーザーと、覚えているログイン名とパスワードも新しくする (新しくしないと、次の起動の照合に失敗して、ログイン画面になる)。
/// ほかの PC が覚えている古いパスワードは、その PC の次の起動で合わなくなり、ログイン画面になる (正しい動き)。
/// </remarks>
public sealed class UserAccountService(IAppUserRepository users, ISavedCredentialStore saved, CurrentUser currentUser)
{
    /// <summary>表示名を変える</summary>
    /// <param name="displayName">新しい表示名</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更の完了を表すタスク</returns>
    /// <exception cref="InvalidOperationException">今のユーザーを特定していない (呼ぶ側の順序のバグ)。</exception>
    /// <exception cref="ArgumentException">表示名が空か長すぎる (呼ぶ側が先に確かめる。バグの合図)。</exception>
    public async Task ChangeDisplayNameAsync(string displayName, CancellationToken cancellationToken = default)
    {
        var updated = GetUser() with { DisplayName = displayName.Trim() };
        await users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        currentUser.Set(updated);
    }

    /// <summary>ログイン名を変える</summary>
    /// <param name="loginName">新しいログイン名</param>
    /// <param name="password">今のパスワード (本人の確認)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変えたら true。今のパスワードが合わないときは false</returns>
    /// <exception cref="InvalidOperationException">今のユーザーを特定していない (呼ぶ側の順序のバグ)。</exception>
    /// <exception cref="ArgumentException">ログイン名が空か長すぎる・制御文字がある (呼ぶ側が先に確かめる。バグの合図)。</exception>
    /// <exception cref="LoginNameTakenException">ログイン名がすでに使われている。</exception>
    /// <exception cref="MmmSdk.Core.Components.Secrets.SecretStoreException">変更は済んだが、新しいログイン名を覚えられなかった (次の起動でログインし直しになる)。</exception>
    public async Task<bool> ChangeLoginNameAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        var user = GetUser();
        if (!await VerifyPasswordAsync(user, password, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var updated = user with { LoginName = loginName.Trim() };
        await users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        currentUser.Set(updated);
        saved.Save(new SavedCredential(updated.LoginName, password));
        return true;
    }

    /// <summary>パスワードを変える</summary>
    /// <param name="currentPassword">今のパスワード (本人の確認)</param>
    /// <param name="newPassword">新しいパスワード</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変えたら true。今のパスワードが合わないときは false</returns>
    /// <exception cref="InvalidOperationException">今のユーザーを特定していない、または変える前に削除された。</exception>
    /// <exception cref="ArgumentException">新しいパスワードが空 (呼ぶ側が先に確かめる。バグの合図)。</exception>
    /// <exception cref="MmmSdk.Core.Components.Secrets.SecretStoreException">変更は済んだが、新しいパスワードを覚えられなかった (次の起動でログインし直しになる)。</exception>
    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = GetUser();
        if (!await VerifyPasswordAsync(user, currentPassword, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // ハッシュの計算は時間がかかるので、UI スレッドの外で行う
        var hash = await Task.Run(() => PasswordHasher.Hash(newPassword), cancellationToken).ConfigureAwait(false);
        if (!await users.ChangePasswordHashAsync(user.Id, hash, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("ユーザーが見つかりません。");
        }

        saved.Save(new SavedCredential(user.LoginName, newPassword));
        return true;
    }

    /// <summary>ログアウトする (覚えているログイン名とパスワードを消す)</summary>
    /// <remarks>今のユーザーは、そのままにする。呼ぶ側が、このあとアプリを終了する (次の起動で、ログイン画面になる)。</remarks>
    public void SignOut() => saved.Clear();

    /// <summary>今のユーザーを取得する</summary>
    /// <returns>今のユーザー</returns>
    private AppUser GetUser() => currentUser.User ?? throw new InvalidOperationException("今のユーザーが特定されていません。");

    /// <summary>パスワードが、そのユーザーのものか確かめる</summary>
    /// <param name="user">ユーザー</param>
    /// <param name="password">入力されたパスワード</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>合えば true</returns>
    private async Task<bool> VerifyPasswordAsync(AppUser user, string password, CancellationToken cancellationToken)
    {
        var found = await users.FindByLoginNameAsync(user.LoginName, cancellationToken).ConfigureAwait(false);
        return found is not null && found.PasswordHash.Length > 0 && PasswordHasher.Verify(password, found.PasswordHash);
    }
}
