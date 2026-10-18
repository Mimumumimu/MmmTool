namespace MmmToolWeb.Auth.Core.Enrollment;

/// <summary>
/// 発行した登録用コード。
/// </summary>
/// <param name="Code">コード (8 桁の数字)。発行の直後にだけ分かる。</param>
/// <param name="ExpiresAt">コードの期限。</param>
public sealed record IssuedEnrollmentCode(string Code, DateTimeOffset ExpiresAt);
