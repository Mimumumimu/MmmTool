using System.Security.Cryptography;

namespace MmmTool.Users.Core;

/// <summary>
/// パスワードのハッシュを作り、照合する (PBKDF2-SHA256)。
/// </summary>
/// <remarks>
/// 形は <c>pbkdf2-sha256$&lt;回数&gt;$&lt;ソルト&gt;$&lt;ハッシュ&gt;</c>(ソルト・ハッシュは Base64)。方式と回数を一緒に持つので、あとで強くできる。
/// 回数は OWASP の推奨値 (PBKDF2-HMAC-SHA256 で 600,000 回)。
/// </remarks>
public static class PasswordHasher
{
    /// <summary>方式の名前</summary>
    private const string Scheme = "pbkdf2-sha256";

    /// <summary>反復の回数</summary>
    private const int Iterations = 600_000;

    /// <summary>ソルトのバイト数</summary>
    private const int SaltSize = 16;

    /// <summary>ハッシュのバイト数</summary>
    private const int HashSize = 32;

    /// <summary>パスワードのハッシュを作る</summary>
    /// <param name="password">パスワード</param>
    /// <returns>ハッシュ (毎回違うソルトを使うので、同じパスワードでも違う値になる)</returns>
    /// <exception cref="ArgumentException">パスワードが空 (空のハッシュは「新しく決める」状態を表すため、空のパスワードは使えない)。</exception>
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
        {
            throw new ArgumentException("パスワードを入力してください。", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>パスワードがハッシュと合うか調べる</summary>
    /// <param name="password">入力されたパスワード</param>
    /// <param name="stored">保存されているハッシュ</param>
    /// <returns>合えば true。ハッシュが空・読めない形のときは false</returns>
    public static bool Verify(string password, string stored)
    {
        ArgumentNullException.ThrowIfNull(password);

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
