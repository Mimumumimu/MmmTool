using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Settings;

namespace MmmTool.Backlog.Core;

/// <summary>Backlog 連携の設定</summary>
/// <param name="settings">汎用設定ストア</param>
/// <param name="secrets">秘密の保管庫</param>
/// <remarks>API キーは秘密の保管庫、連携用パスは汎用設定ストアに保存・取得する。</remarks>
public sealed class BacklogSettingsService(ISettingsStore settings, ISecretStore secrets)
{
    /// <summary>連携用パスの設定キー</summary>
    private const string UrlKey = "Backlog.Url";

    /// <summary>API キーを保存する秘密の保管庫の項目の名前</summary>
    /// <remarks>
    /// Windows の資格情報マネージャーではこの名前の汎用資格情報として見える
    /// 変えると保存済みの API キーを読めなくなる
    /// </remarks>
    public const string ApiKeySecretName = "MmmTool.Backlog";

    /// <summary>最後に使った連携用パスで、URL</summary>
    /// <remarks>無ければ空</remarks>
    public string Url => settings.Get(UrlKey, "");

    /// <summary>連携用パスを保存できない状態か</summary>
    /// <remarks>設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている。</remarks>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>連携用パスを履歴を持たず最後の 1 件として上書き保存する</summary>
    /// <param name="url">保存する連携用パス (URL)</param>
    /// <returns>
    /// 保存したら true
    /// 設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false
    /// </returns>
    public Task<bool> SetUrlAsync(string url) => settings.SetAsync(UrlKey, url);

    /// <summary>登録済みの API キーを取得する</summary>
    /// <returns>
    /// API キー
    /// 登録が無ければ null
    /// </returns>
    /// <exception cref="SecretStoreException">秘密の保管庫を読めなかった</exception>
    public string? GetApiKey() => secrets.Get(ApiKeySecretName);

    /// <summary>API キーを登録する</summary>
    /// <param name="apiKey">登録する API キー</param>
    /// <remarks>空なら、登録を消す。</remarks>
    /// <exception cref="SecretStoreException">秘密の保管庫に書けなかった</exception>
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
