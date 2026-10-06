namespace MmmTool.Users.Core;

/// <summary>
/// このアプリを今使っている人。アプリ全体で 1 つ。
/// </summary>
/// <remarks>
/// 起動時の準備で、この PC の MAC から特定して設定する (<see cref="UserIdentificationService"/>)。DB の保存先 (DB モード)だけが使う。
/// ローカルモードでは、特定しない (誰も設定されない)。
/// </remarks>
public sealed class CurrentUser
{
    private volatile AppUser? _user;

    /// <summary>今のユーザーが設定された (特定した・登録した)</summary>
    /// <remarks>設定したスレッドから発火する (任意のスレッドになりうる)。保存先が、すぐに読み直すために使う。</remarks>
    public event EventHandler? Changed;

    /// <summary>今のユーザー。まだ特定していなければ null</summary>
    public AppUser? User => _user;

    /// <summary>今のユーザーを特定済みか</summary>
    public bool IsIdentified => _user is not null;

    /// <summary>今のユーザーの番号</summary>
    /// <exception cref="InvalidOperationException">まだ特定していない (呼ぶ側の順序のバグ)。</exception>
    public int Id => _user?.Id ?? throw new InvalidOperationException("今のユーザーが特定されていません。");

    /// <summary>今のユーザーを設定する</summary>
    /// <param name="user">特定したユーザー</param>
    public void Set(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _user = user;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
