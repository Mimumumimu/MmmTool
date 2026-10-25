using System.Text.Json;

namespace MmmToolWeb.Client.Auth;

/// <summary>
/// ログイン中のユーザーの情報。
/// </summary>
/// <param name="Name">表示する名前。</param>
/// <param name="SignedInAt">ログインした日時。</param>
public sealed record MeResponse(string Name, DateTimeOffset SignedInAt);

#if DEBUG
/// <summary>
/// 開発用のビルド (Debug)だけにある、パスワードなしのログインの要求。
/// </summary>
/// <param name="LoginName">ログインする人のログイン名。</param>
public sealed record DevLoginRequest(string LoginName);

#endif
/// <summary>
/// パスキーの登録を始める要求。
/// </summary>
/// <param name="Code">管理者が出した登録用コード。</param>
/// <param name="DeviceName">端末の呼び名。</param>
public sealed record EnrollStartRequest(string Code, string DeviceName);

/// <summary>
/// パスキーの登録・ログインを始めた結果。
/// </summary>
/// <param name="CeremonyId">完了の要求に渡す番号。</param>
/// <param name="Options">ブラウザの <c>navigator.credentials</c> に渡す選択肢。</param>
public sealed record CeremonyStartResponse(string CeremonyId, JsonElement Options);

/// <summary>
/// パスキーの登録を完了する要求。
/// </summary>
/// <param name="Code">管理者が出した登録用コード。</param>
/// <param name="CeremonyId">始めたときに受け取った番号。</param>
/// <param name="Credential">ブラウザが返した結果。</param>
public sealed record EnrollFinishRequest(string Code, string CeremonyId, JsonElement Credential);

/// <summary>
/// パスキーでのログインを完了する要求。
/// </summary>
/// <param name="CeremonyId">始めたときに受け取った番号。</param>
/// <param name="Credential">ブラウザが返した結果。</param>
public sealed record LoginFinishRequest(string CeremonyId, JsonElement Credential);
