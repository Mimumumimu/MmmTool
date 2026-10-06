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

    /// <summary>MAC アドレス (大文字の 16 進 12 桁・区切りなし)</summary>
    public string MacAddress { get; init; } = "";

    /// <summary>使い始める日</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>使い終わる日 (含む)。無期限は <see cref="DateOnly.MaxValue"/></summary>
    public DateOnly ValidTo { get; init; } = DateOnly.MaxValue;

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="user">ユーザー</param>
    /// <returns>DB の行。作成・更新の日時と、作成者・更新者は決めない</returns>
    /// <exception cref="ArgumentException">表示名が空か長すぎる (50 文字まで)、MAC アドレスが 12 桁の大文字の 16 進ではない、使い終わる日が使い始める日より前。</exception>
    public static AppUserRow FromAppUser(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.DisplayName) || user.DisplayName.Length > AppUser.DisplayNameMaxLength)
        {
            throw new ArgumentException($"表示名は、空でない {AppUser.DisplayNameMaxLength} 文字までにしてください。", nameof(user));
        }
        if (user.MacAddress.Length != 12 || !user.MacAddress.All(character => character is (>= '0' and <= '9') or (>= 'A' and <= 'F')))
        {
            throw new ArgumentException($"MAC アドレス {user.MacAddress} は、大文字の 16 進 12 桁ではありません。", nameof(user));
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
            MacAddress = user.MacAddress,
            ValidFrom = user.ValidFrom,
            ValidTo = user.ValidTo,
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>ユーザー</returns>
    public AppUser ToAppUser() => new()
    {
        Id = Id,
        IsDeleted = IsDeleted,
        DisplayName = DisplayName,
        MacAddress = MacAddress,
        ValidFrom = ValidFrom,
        ValidTo = ValidTo,
    };
}
