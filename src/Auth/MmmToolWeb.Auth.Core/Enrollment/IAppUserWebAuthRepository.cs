namespace MmmToolWeb.Auth.Core.Enrollment;

/// <summary>
/// Web 版の認証の状態 (登録用コード・ログインの世代の番号)の保存先。
/// </summary>
/// <remarks>
/// コードの期限は、保存先の時計で判断する。複数のサーバーの時計のずれを入れないため。コードは、ハッシュだけを渡す。
/// </remarks>
public interface IAppUserWebAuthRepository
{
    /// <summary>
    /// 登録用コードを保存する。その人に前のコードがあれば置き換える。
    /// </summary>
    /// <param name="appUserId">コードを出す人のユーザーの番号。</param>
    /// <param name="codeHash">コードのハッシュで、SHA-256 の 16 進</param>
    /// <param name="lifetime">今から有効な時間。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>コードの期限。</returns>
    Task<DateTimeOffset> SetEnrollmentCodeAsync(int appUserId, string codeHash, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>
    /// 有効な登録用コードの持ち主を探す。コードは使い終わりにしない。
    /// </summary>
    /// <param name="codeHash">コードのハッシュで、SHA-256 の 16 進</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>持ち主のユーザーの番号。コードが無い・期限切れのときは <c>null</c>。</returns>
    Task<int?> FindUserByEnrollmentCodeAsync(string codeHash, CancellationToken cancellationToken);

    /// <summary>
    /// 有効な登録用コードを使い終わりにする。
    /// </summary>
    /// <param name="codeHash">コードのハッシュで、SHA-256 の 16 進</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>持ち主のユーザーの番号。コードが無い・期限切れ・すでに使い終わりのときは <c>null</c>。</returns>
    /// <remarks>同じコードを同時に使っても、成功するのは 1 回だけになる。</remarks>
    Task<int?> ConsumeEnrollmentCodeAsync(string codeHash, CancellationToken cancellationToken);

    /// <summary>
    /// その人のログインの世代の番号を読む。
    /// </summary>
    /// <param name="appUserId">ユーザーの番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>世代の番号。行が無い人は 1。</returns>
    Task<int> GetSessionVersionAsync(int appUserId, CancellationToken cancellationToken);

    /// <summary>
    /// その人のログインの世代の番号を 1 上げる。上げる前の番号を持つログインは、すべて無効になる。
    /// </summary>
    /// <param name="appUserId">ユーザーの番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>上げたあとの世代の番号。</returns>
    /// <remarks>行が無い人は、世代 1 として扱っているので、世代 2 の行を作る。</remarks>
    Task<int> IncrementSessionVersionAsync(int appUserId, CancellationToken cancellationToken);
}
