using System.Security.Cryptography;
using System.Text;

namespace MmmToolWeb.Auth.Core.Enrollment;

/// <summary>
/// 登録用コードの発行と確認。コード本体は保存せず、ハッシュだけを保存先に渡す。
/// </summary>
/// <param name="repository">コードの保存先。</param>
public sealed class EnrollmentCodeService(IAppUserWebAuthRepository repository)
{
    /// <summary>コードの桁数。</summary>
    private const int Digits = 8;

    /// <summary>コードを作り直す最大の回数 (ほかの人の有効なコードと重なったとき)。</summary>
    private const int MaxAttempts = 5;

    /// <summary>コードが使える時間。</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 新しい登録用コードを発行する。その人に前のコードがあれば使えなくなる。
    /// </summary>
    /// <param name="appUserId">コードを出す人のユーザーの番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>発行したコードと期限。</returns>
    /// <exception cref="InvalidOperationException">ほかの人の有効なコードと重ならないコードを作れなかったとき。</exception>
    public async Task<IssuedEnrollmentCode> IssueAsync(int appUserId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(0, 100_000_000).ToString($"D{Digits}");
            var hash = Hash(code);
            var owner = await repository.FindUserByEnrollmentCodeAsync(hash, cancellationToken);
            if (owner is not null && owner != appUserId)
            {
                continue;
            }
            var expiresAt = await repository.SetEnrollmentCodeAsync(appUserId, hash, Lifetime, cancellationToken);
            return new IssuedEnrollmentCode(code, expiresAt);
        }
        throw new InvalidOperationException("登録用コードを作れませんでした。もう一度実行してください。");
    }

    /// <summary>
    /// 有効な登録用コードの持ち主を探す (コードは使い終わりにしない)。
    /// </summary>
    /// <param name="code">入力されたコード。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>持ち主のユーザーの番号。コードが違う・期限切れのときは <c>null</c>。</returns>
    public async Task<int?> FindUserAsync(string code, CancellationToken cancellationToken) =>
        IsWellFormed(code) ? await repository.FindUserByEnrollmentCodeAsync(Hash(code), cancellationToken) : null;

    /// <summary>
    /// 有効な登録用コードを使い終わりにする。
    /// </summary>
    /// <param name="code">入力されたコード。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>持ち主のユーザーの番号。コードが違う・期限切れ・すでに使い終わりのときは <c>null</c>。</returns>
    public async Task<int?> ConsumeAsync(string code, CancellationToken cancellationToken) =>
        IsWellFormed(code) ? await repository.ConsumeEnrollmentCodeAsync(Hash(code), cancellationToken) : null;

    /// <summary>
    /// 入力が、コードの形 (数字だけの決まった桁数)かを確かめる。
    /// </summary>
    /// <param name="code">入力されたコード。</param>
    /// <returns>コードの形なら <c>true</c>。</returns>
    private static bool IsWellFormed(string code) => code.Length == Digits && code.All(char.IsAsciiDigit);

    /// <summary>
    /// コードのハッシュ (SHA-256 の 16 進、小文字)を作る。
    /// </summary>
    /// <param name="code">コード。</param>
    /// <returns>ハッシュ。</returns>
    private static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
