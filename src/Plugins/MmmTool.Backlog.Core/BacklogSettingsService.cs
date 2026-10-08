using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Settings;

namespace MmmTool.Backlog.Core;

/// <summary>
/// Backlog 連携の設定。API キーは秘密の保管庫、連携用パス (URL)は汎用設定ストアに保存・取得する。
/// </summary>
/// <param name="settings">汎用設定ストア</param>
/// <param name="secrets">秘密の保管庫</param>
public sealed class BacklogSettingsService(ISettingsStore settings, ISecretStore secrets)
{
    /// <summary>連携用パス (URL)の設定キー</summary>
    private const string UrlKey = "Backlog.Url";

    /// <summary>API キーを保存する、秘密の保管庫の項目の名前</summary>
    /// <remarks>Windows の資格情報マネージャーでは、この名前の汎用資格情報として見える。変えると、保存済みの API キーを読めなくなる。</remarks>
    public const string ApiKeySecretName = "MmmTool.Backlog";

    /// <summary>最後に使った連携用パス (URL)。無ければ空</summary>
    public string Url => settings.Get(UrlKey, "");

    /// <summary>連携用パス (URL)を保存できない状態か (設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている)</summary>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>連携用パス (URL)を、履歴を持たず最後の 1 件として上書き保存する</summary>
    /// <param name="url">保存する連携用パス (URL)</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public Task<bool> SetUrlAsync(string url) => settings.SetAsync(UrlKey, url);

    /// <summary>登録済みの API キーを取得する</summary>
    /// <returns>API キー。登録が無ければ null</returns>
    /// <exception cref="SecretStoreException">秘密の保管庫を読めなかった。</exception>
    public string? GetApiKey() => secrets.Get(ApiKeySecretName);

    /// <summary>API キーを登録する。空なら、登録を消す</summary>
    /// <param name="apiKey">登録する API キー</param>
    /// <exception cref="SecretStoreException">秘密の保管庫に書けなかった。</exception>
    public void SetApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            secrets.Remove(ApiKeySecretName);
        }
        else
        {
            secrets.Set(ApiKeySecretName, apiKey);
        }
    }
}
