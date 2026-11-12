namespace MmmTool.Users.Core;

/// <summary>ログイン名とパスワードで今のユーザーを特定する</summary>
/// <param name="users">ユーザーの保存先</param>
/// <param name="saved">覚えているログイン名とパスワード</param>
/// <param name="currentUser">今のユーザー (特定したら設定する)</param>
public sealed class UserSignInService(IAppUserRepository users, ISavedCredentialStore saved, CurrentUser currentUser)
{
    /// <summary>覚えているログイン名とパスワードで、ログインを試す</summary>
    /// <param name="today">今日 (使える期間の判定に使う)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// ログインしたユーザー
    /// 覚えていない・合わなかったときは null (合わなかったときは覚えている内容を消す)
    /// </returns>
    /// <remarks>
    /// 覚えていないときも DB へつなげるか確かめる。
    /// つなげないとき (設定が足りない・届かない)は例外のままにして、覚えている内容を消さない。
    /// あとでもう一度試せるようにするため。
    /// </remarks>
    public async Task<AppUser?> TrySignInWithSavedAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var credential = saved.Load();
        if (credential is null)
        {
            // 覚えていなくても、先に DB へつなげるか確かめる。つなげないとき、ログインの画面を出さずに、保存先が知らせるため。
            // 存在しない番号 0 を 1 件引く。全ユーザーを読まず、つなげるかと表があるかだけを、主キーの 1 回の引きで確かめる
            await users.GetUsersByIdsAsync([0], cancellationToken).ConfigureAwait(false);
            return null;
        }

        var result = await SignInAsync(credential.LoginName, credential.Password, today, cancellationToken).ConfigureAwait(false);
        if (result.Status == SignInStatus.Succeeded)
        {
            return result.User;
        }

        saved.Clear();
        return null;
    }

    /// <summary>ログイン名とパスワードでログインする</summary>
    /// <param name="loginName">ログイン名</param>
    /// <param name="password">パスワード</param>
    /// <param name="today">今日 (使える期間の判定に使う)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 結果。
    /// 成功したら、今のユーザーに設定し、ログイン名とパスワードを覚える。
    /// </returns>
    public async Task<SignInResult> SignInAsync(string loginName, string password, DateOnly today, CancellationToken cancellationToken = default)
    {
        var found = await FindActiveAsync(loginName, today, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return new SignInResult(SignInStatus.Failed, null);
        }
        if (found.PasswordHash.Length == 0)
        {
            return new SignInResult(SignInStatus.NeedsNewPassword, null);
        }
        if (!PasswordHasher.Verify(password, found.PasswordHash))
        {
            return new SignInResult(SignInStatus.Failed, null);
        }

        Complete(found.User, password);
        return new SignInResult(SignInStatus.Succeeded, found.User);
    }

    /// <summary>パスワードが空のユーザーに新しいパスワードを決めてログインする</summary>
    /// <param name="loginName">ログイン名</param>
    /// <param name="newPassword">新しいパスワード</param>
    /// <param name="today">今日 (使える期間の判定に使う)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// ログインしたユーザー
    /// パスワードがすでにある (ほかの人が先に決めた)・ユーザーが無いときは null
    /// </returns>
    /// <exception cref="ArgumentException">パスワードが空</exception>
    public async Task<AppUser?> SetNewPasswordAsync(string loginName, string newPassword, DateOnly today, CancellationToken cancellationToken = default)
    {
        var hash = await HashAsync(newPassword).ConfigureAwait(false);
        var found = await FindActiveAsync(loginName, today, cancellationToken).ConfigureAwait(false);
        if (found is null || found.PasswordHash.Length != 0
            || !await users.SetPasswordHashAsync(found.User.Id, hash, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        Complete(found.User, newPassword);
        return found.User;
    }

    /// <summary>ユーザーを新しく登録してログインする</summary>
    /// <param name="loginName">ログイン名</param>
    /// <param name="displayName">表示名</param>
    /// <param name="password">パスワード</param>
    /// <param name="today">今日 (使い始める日になる)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>登録したユーザー</returns>
    /// <exception cref="ArgumentException">
    /// ログイン名・表示名・パスワードが正しくない。
    /// 呼ぶ側が先に確かめるので、バグの合図。
    /// </exception>
    /// <exception cref="LoginNameTakenException">ログイン名がすでに使われている</exception>
    public async Task<AppUser> RegisterAsync(string loginName, string displayName, string password, DateOnly today, CancellationToken cancellationToken = default)
    {
        var hash = await HashAsync(password).ConfigureAwait(false);
        var user = await users
            .AddAsync(new AppUser { LoginName = loginName.Trim(), DisplayName = displayName.Trim(), ValidFrom = today }, hash, cancellationToken)
            .ConfigureAwait(false);
        Complete(user, password);
        return user;
    }

    /// <summary>パスワードのハッシュを UI スレッドの外で作る</summary>
    /// <param name="password">パスワード</param>
    /// <returns>ハッシュ</returns>
    /// <remarks>計算に時間がかかり、画面が固まるため。</remarks>
    private static Task<string> HashAsync(string password) => Task.Run(() => PasswordHasher.Hash(password));

    /// <summary>ログイン名から、その日に使えるユーザーを探す</summary>
    /// <param name="loginName">ログイン名</param>
    /// <param name="today">今日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 見つかった内容
    /// 無い・使えないときは null
    /// </returns>
    private async Task<AppUserCredential?> FindActiveAsync(string loginName, DateOnly today, CancellationToken cancellationToken)
    {
        var found = await users.FindByLoginNameAsync(loginName.Trim(), cancellationToken).ConfigureAwait(false);
        return found is not null && found.User.IsActiveOn(today) ? found : null;
    }

    /// <summary>ログインの成功を反映する</summary>
    /// <param name="user">ログインしたユーザー</param>
    /// <param name="password">パスワード</param>
    /// <remarks>今のユーザーに設定し、ログイン名とパスワードを覚える。</remarks>
    private void Complete(AppUser user, string password)
    {
        saved.Save(new SavedCredential(user.LoginName, password));
        currentUser.Set(user);
    }
}

/// <summary>ログインの結果の種類</summary>
public enum SignInStatus
{
    /// <summary>ログインできた</summary>
    Succeeded,

    /// <summary>ログイン名かパスワードが合わない</summary>
    /// <remarks>ユーザーが無い・使えない場合も含む</remarks>
    Failed,

    /// <summary>パスワードが空で、管理者が再設定した</summary>
    /// <remarks>新しいパスワードを決める</remarks>
    NeedsNewPassword,
}

/// <summary>ログインの結果</summary>
/// <param name="Status">結果の種類</param>
/// <param name="User">
/// ログインしたユーザー
/// 成功したときだけ
/// </param>
public sealed record SignInResult(SignInStatus Status, AppUser? User);
