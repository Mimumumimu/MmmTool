using MmmTool.Users.Core;

namespace MmmTool.Data.Users;

/// <summary>
/// DB の <c>dbo.AppUser</c> の 1 行 (列と 1 対 1)。アプリの型 <see cref="AppUser"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 無期限の使い終わる日は <see cref="DateOnly.MaxValue"/> (<c>9999-12-31</c>)で、アプリの型と同じ。NULL は使わない (理由は docs/specs/database.md)。
/// 変換は <see cref="FromAppUser"/> と <see cref="ToAppUser"/>。
/// </remarks>
public sealed record AppUserRow
{
    /// <summary>主キー (<see cref="AppUser.Id"/>)。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    /// <remarks>DB の既定値で入る。<see cref="FromAppUser"/> では決めない。</remarks>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (<c>AppUser.Id</c>)</summary>
    /// <remarks>本人の登録なので、番号がまだ無く 0。<see cref="FromAppUser"/> では決めない。</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    /// <remarks>更新の SQL の中で、DB サーバーの時計を使って書く。<see cref="FromAppUser"/> では決めない。</remarks>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<c>AppUser.Id</c>)</summary>
    /// <remarks>本人の登録なので、番号がまだ無く 0。<see cref="FromAppUser"/> では決めない。</remarks>
    public int UpdatedByUserId { get; init; }

    /// <summary>表示名</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>ログイン名</summary>
    public string LoginName { get; init; } = "";

    /// <summary>パスワードのハッシュ。空文字は、新しく決める状態</summary>
    public string PasswordHash { get; init; } = "";

    /// <summary>使い始める日</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>使い終わる日 (含む)。無期限は <see cref="DateOnly.MaxValue"/></summary>
    public DateOnly ValidTo { get; init; } = DateOnly.MaxValue;

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="user">ユーザー</param>
    /// <param name="passwordHash">パスワードのハッシュ</param>
    /// <returns>DB の行。作成・更新の日時と、作成者・更新者は決めない</returns>
    /// <exception cref="ArgumentException">表示名・ログイン名が空か長すぎる (50 文字まで)、ログイン名に制御文字がある、使い終わる日が使い始める日より前。</exception>
    public static AppUserRow FromAppUser(AppUser user, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.DisplayName) || user.DisplayName.Length > AppUser.DisplayNameMaxLength)
        {
            throw new ArgumentException($"表示名は、空でない {AppUser.DisplayNameMaxLength} 文字までにしてください。", nameof(user));
        }
        if (string.IsNullOrWhiteSpace(user.LoginName) || user.LoginName.Length > AppUser.LoginNameMaxLength || user.LoginName.Any(char.IsControl))
        {
            throw new ArgumentException($"ログイン名は、空でない {AppUser.LoginNameMaxLength} 文字までにしてください (改行などは使えません)。", nameof(user));
        }
        if (user.ValidTo < user.ValidFrom)
        {
            throw new ArgumentException("使い終わる日が、使い始める日より前です。", nameof(user));
        }

        return new AppUserRow
        {
            Id = user.Id,
            IsDeleted = user.IsDeleted,
            DisplayName = user.DisplayName,
            LoginName = user.LoginName,
            PasswordHash = passwordHash,
            ValidFrom = user.ValidFrom,
            ValidTo = user.ValidTo,
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>ユーザー (パスワードのハッシュは含まない)</returns>
    public AppUser ToAppUser() => new()
    {
        Id = Id,
        IsDeleted = IsDeleted,
        DisplayName = DisplayName,
        LoginName = LoginName,
        ValidFrom = ValidFrom,
        ValidTo = ValidTo,
    };
}
