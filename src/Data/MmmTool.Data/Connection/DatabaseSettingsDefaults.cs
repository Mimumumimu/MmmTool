namespace MmmTool.Data.Connection;

/// <summary>
/// アプリごとに違う、DB への接続の設定の初期値と、パスワードの保存名。
/// </summary>
/// <param name="DatabaseName">データベース名の初期値 (保存されていないとき)</param>
/// <param name="UserName">ユーザー名の初期値 (保存されていないとき)。アプリごとに、専用のログインを使う</param>
/// <param name="PasswordSecretName">パスワードを保存する、秘密の保管庫の項目の名前</param>
/// <remarks>
/// 同じ Windows アカウントで複数のアプリを使うと、保存名が同じでは、保存したパスワードを上書きし合う。
/// 保存名は Windows の資格情報マネージャーでは、この名前の汎用資格情報として見える。変えると、保存済みのパスワードを読めなくなる。
/// </remarks>
public sealed record DatabaseSettingsDefaults(string DatabaseName, string UserName, string PasswordSecretName)
{
    /// <summary>MmmTool の既定 (データベース名・ユーザー名は <c>MmmTool</c>、保存名は <c>MmmTool.Database</c>)</summary>
    public static DatabaseSettingsDefaults MmmTool { get; } = new("MmmTool", "MmmTool", "MmmTool.Database");
}
