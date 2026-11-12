namespace MmmToolWeb.Auth.Core.Passkeys;

/// <summary>1 人が登録した 1 つのパスキー</summary>
/// <param name="Id">パスキーの番号</param>
/// <param name="AppUserId">持ち主のユーザーの番号</param>
/// <param name="CredentialId">パスキーの識別番号で、認証器が決める。</param>
/// <param name="PublicKey">
/// 公開鍵
/// 署名の検証に使う
/// </param>
/// <param name="SignCount">
/// 認証器が数えた使用回数。
/// 前回より増えていないとき、複製を疑う。
/// </param>
/// <param name="UserHandle">パスキーの側が持つ、本人の印で、ログイン名と別のランダムな値。</param>
/// <param name="DeviceName">端末の呼び名で、「iPhone」など。</param>
/// <param name="LastUsedAt">最後に使った日時で、UTC。</param>
public sealed record AppUserPasskey(
    int Id,
    int AppUserId,
    byte[] CredentialId,
    byte[] PublicKey,
    uint SignCount,
    byte[] UserHandle,
    string DeviceName,
    DateTime LastUsedAt);
