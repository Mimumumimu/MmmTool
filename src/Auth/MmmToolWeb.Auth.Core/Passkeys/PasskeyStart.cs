namespace MmmToolWeb.Auth.Core.Passkeys;

/// <summary>
/// パスキーの登録・ログインの最初の問い合わせの結果。
/// </summary>
/// <param name="CeremonyId">完了の呼び出しに渡す番号。</param>
/// <param name="OptionsJson">ブラウザの <c>navigator.credentials</c> に渡す選択肢 (JSON)。</param>
public sealed record PasskeyStart(string CeremonyId, string OptionsJson);
