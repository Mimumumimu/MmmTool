using MmmSdk.Core.Components.Secrets;
using MmmTool.Data.Connection;
using MmmTool.Users.Core;

namespace MmmTool.Data.Users;

/// <summary>
/// ログイン名とパスワードを、秘密の保管庫 (Windows の資格情報マネージャー。Windows アカウントごと)に覚える。
/// </summary>
/// <remarks>
/// 項目の名前は <c>MmmTool.Account.&lt;サーバー名&gt;/&lt;データベース名&gt;</c>。接続先が違うと別の項目になり、互いを上書きしない。
/// 値は「ログイン名、改行、パスワード」の 1 つの文字列 (ログイン名に改行などの制御文字は使えない。<see cref="AppUserRow.FromAppUser"/>)。
/// </remarks>
/// <param name="secrets">秘密の保管庫</param>
/// <param name="settings">DB への接続の設定 (項目の名前に使う)</param>
public sealed class SecretSavedCredentialStore(ISecretStore secrets, DatabaseSettingsService settings) : ISavedCredentialStore
{
    /// <summary>項目の名前の先頭</summary>
    private const string NamePrefix = "MmmTool.Account.";

    /// <inheritdoc />
    /// <remarks>保管庫を読めないときは、覚えていないものとして扱う (ログイン画面を出す)。</remarks>
    public SavedCredential? Load()
    {
        string? value;
        try
        {
            value = secrets.Get(GetName());
        }
        catch (SecretStoreException)
        {
            return null;
        }

        var parts = value?.Split('\n', 2);
        return parts is { Length: 2 } ? new SavedCredential(parts[0], parts[1]) : null;
    }

    /// <inheritdoc />
    /// <exception cref="SecretStoreException">保管庫に書けなかった。</exception>
    public void Save(SavedCredential credential) => secrets.Set(GetName(), $"{credential.LoginName}\n{credential.Password}");

    /// <inheritdoc />
    /// <exception cref="SecretStoreException">保管庫から消せなかった。</exception>
    public void Clear() => secrets.Remove(GetName());

    /// <summary>今の接続先の、項目の名前を作る</summary>
    /// <returns>項目の名前</returns>
    private string GetName()
    {
        var connection = settings.Load();
        return $"{NamePrefix}{connection.Server}/{connection.Name}";
    }
}
