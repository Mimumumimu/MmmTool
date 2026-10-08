using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Settings;

namespace MmmTool.Data.Connection;

/// <summary>
/// DB への接続の設定。パスワードは秘密の保管庫、それ以外は汎用設定ストアに保存・取得する。
/// </summary>
/// <remarks>
/// 設定のキーは <c>Database.</c> で始める (<c>Mode</c>・<c>Server</c>・<c>Name</c>・<c>Authentication</c>・<c>UserName</c>・<c>TrustServerCertificate</c>)。
/// 種類 (<see cref="DatabaseMode"/>・<see cref="DatabaseAuthentication"/>)は名前で保存し、手で直した値などで読めないときは既定値 (JSON・ユーザー名とパスワード)にする。
/// </remarks>
/// <param name="settings">汎用設定ストア</param>
/// <param name="secrets">秘密の保管庫</param>
/// <param name="defaults">アプリごとの、データベース名・ユーザー名の初期値と、パスワードの保存名</param>
public sealed class DatabaseSettingsService(ISettingsStore settings, ISecretStore secrets, DatabaseSettingsDefaults defaults)
{
    private const string ModeKey = "Database.Mode";
    private const string ServerKey = "Database.Server";
    private const string NameKey = "Database.Name";
    private const string AuthenticationKey = "Database.Authentication";
    private const string UserNameKey = "Database.UserName";
    private const string TrustServerCertificateKey = "Database.TrustServerCertificate";

    /// <summary>設定を保存できない状態か (設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている)</summary>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>保存先の種類が、保存されているか</summary>
    /// <remarks>false なら、まだ保存先を選んでいない (初回)。</remarks>
    public bool IsModeSaved => settings.Contains(ModeKey);

    /// <summary>今の接続の設定を取得する</summary>
    /// <returns>接続の設定。保存されていない項目は既定値 (データベース名とユーザー名は <c>MmmTool</c>)</returns>
    public DatabaseSettings Load() => new()
    {
        Mode = GetEnum(ModeKey, DatabaseMode.Json),
        Server = settings.Get(ServerKey, ""),
        Name = settings.Get(NameKey, defaults.DatabaseName),
        Authentication = GetEnum(AuthenticationKey, DatabaseAuthentication.Sql),
        UserName = settings.Get(UserNameKey, defaults.UserName),
        TrustServerCertificate = settings.Get(TrustServerCertificateKey, false),
    };

    /// <summary>接続の設定を保存する</summary>
    /// <param name="value">保存する接続の設定</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>すべて保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public async Task<bool> SaveAsync(DatabaseSettings value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (IsReadOnly)
        {
            return false;
        }

        var saved = await settings.SetAsync(ModeKey, value.Mode.ToString(), cancellationToken).ConfigureAwait(false)
            && await settings.SetAsync(ServerKey, value.Server, cancellationToken).ConfigureAwait(false)
            && await settings.SetAsync(NameKey, value.Name, cancellationToken).ConfigureAwait(false)
            && await settings.SetAsync(AuthenticationKey, value.Authentication.ToString(), cancellationToken).ConfigureAwait(false)
            && await settings.SetAsync(UserNameKey, value.UserName, cancellationToken).ConfigureAwait(false)
            && await settings.SetAsync(TrustServerCertificateKey, value.TrustServerCertificate, cancellationToken).ConfigureAwait(false);
        return saved;
    }

    /// <summary>登録済みのパスワードを取得する</summary>
    /// <returns>パスワード。登録が無ければ null</returns>
    /// <exception cref="SecretStoreException">秘密の保管庫を読めなかった。</exception>
    public string? GetPassword() => secrets.Get(defaults.PasswordSecretName);

    /// <summary>パスワードを登録する。空なら、登録を消す</summary>
    /// <param name="password">登録するパスワード</param>
    /// <exception cref="SecretStoreException">秘密の保管庫に書けなかった。</exception>
    public void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            secrets.Remove(defaults.PasswordSecretName);
        }
        else
        {
            secrets.Set(defaults.PasswordSecretName, password);
        }
    }

    /// <summary>設定の名前から、種類を取得する</summary>
    /// <typeparam name="T">種類の型</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・読めないときに返す種類</param>
    /// <returns>保存されている種類。無い・名前が合わないときは既定の種類</returns>
    private T GetEnum<T>(string key, T defaultValue) where T : struct, Enum
        => Enum.TryParse<T>(settings.Get(key, ""), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : defaultValue;
}
