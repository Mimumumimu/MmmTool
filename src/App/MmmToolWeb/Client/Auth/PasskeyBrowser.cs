using System.Text.Json;
using Microsoft.JSInterop;

namespace MmmToolWeb.Client.Auth;

/// <summary>
/// ブラウザのパスキー (WebAuthn)の呼び出し。
/// </summary>
/// <param name="js">JavaScript の呼び出し口。</param>
public sealed class PasskeyBrowser(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>読み込んだ JavaScript のモジュール。</summary>
    private IJSObjectReference? _module;

    /// <summary>
    /// パスキーを新しく作る。
    /// </summary>
    /// <param name="options">サーバーが返した選択肢。</param>
    /// <returns>ブラウザが返した結果。作らずに閉じられたときは <c>null</c>。</returns>
    public async Task<JsonElement?> CreateAsync(JsonElement options) =>
        await (await ModuleAsync()).InvokeAsync<JsonElement?>("createCredential", options);

    /// <summary>
    /// 登録済みのパスキーで署名する。
    /// </summary>
    /// <param name="options">サーバーが返した選択肢。</param>
    /// <returns>ブラウザが返した結果。使わずに閉じられたときは <c>null</c>。</returns>
    public async Task<JsonElement?> GetAsync(JsonElement options) =>
        await (await ModuleAsync()).InvokeAsync<JsonElement?>("getCredential", options);

    /// <summary>
    /// パスキーが使えるブラウザかを返す。
    /// </summary>
    /// <returns>使えるなら <c>true</c>。</returns>
    public async Task<bool> IsSupportedAsync() => await (await ModuleAsync()).InvokeAsync<bool>("isSupported");

    /// <summary>
    /// ホーム画面のアプリとして開いているかを返す。
    /// </summary>
    /// <returns>ホーム画面のアプリなら <c>true</c>。</returns>
    public async Task<bool> IsStandaloneAsync() => await (await ModuleAsync()).InvokeAsync<bool>("isStandalone");

    /// <summary>
    /// 端末の呼び名を返す。
    /// </summary>
    /// <returns>端末の呼び名。</returns>
    public async Task<string> DeviceNameAsync() => await (await ModuleAsync()).InvokeAsync<string>("deviceName");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    /// <summary>
    /// JavaScript のモジュールを、初めて使うときに読み込む。
    /// </summary>
    /// <returns>読み込んだモジュール。</returns>
    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/webauthn.js");
}
