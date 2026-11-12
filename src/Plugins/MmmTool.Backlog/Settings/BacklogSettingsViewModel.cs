using MmmSdk.Core.Components.Secrets;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Backlog.Core;

namespace MmmTool.Backlog.Settings;

/// <summary>Backlog 連携の設定の ViewModel</summary>
public sealed class BacklogSettingsViewModel
{
    /// <summary>Backlog 連携の設定</summary>
    private readonly BacklogSettingsService _backlogSettings;

    /// <summary>保存済みの API キー</summary>
    /// <remarks>変わっていないときは保存し直さない</remarks>
    private string _savedApiKey = "";

    /// <summary>画面に出すエラー</summary>
    /// <remarks>API キーを読めなかった・保存できなかったとき</remarks>
    public ErrorState Error { get; } = new();

    /// <summary>入力欄の最初の値で、保存済みの API キー</summary>
    /// <remarks>登録が無ければ空</remarks>
    public string InitialApiKey => _savedApiKey;

    /// <summary>保存済みの API キーを読み込む</summary>
    /// <param name="backlogSettings">Backlog 連携の設定</param>
    public BacklogSettingsViewModel(BacklogSettingsService backlogSettings)
    {
        _backlogSettings = backlogSettings;
        try
        {
            _savedApiKey = backlogSettings.GetApiKey() ?? "";
        }
        catch (SecretStoreException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>API キーを保存する</summary>
    /// <param name="apiKey">
    /// 入力された API キー。
    /// 空なら、登録を消す。
    /// </param>
    /// <remarks>
    /// 入力欄からフォーカスが外れたときに呼ぶ
    /// 前後の空白は除く
    /// 貼り付けたときに混ざりやすいため
    /// 変わっていなければ何もしない
    /// </remarks>
    public void Save(string apiKey)
    {
        apiKey = apiKey.Trim();
        if (apiKey == _savedApiKey)
        {
            return;
        }

        try
        {
            _backlogSettings.SetApiKey(apiKey);
            _savedApiKey = apiKey;
            Error.Clear();
        }
        catch (SecretStoreException ex)
        {
            Error.Show(ex.Message);
        }
    }
}
