using Microsoft.JSInterop;

namespace MmmToolWeb.Features.Client;

/// <summary>ブラウザ側で始めた購読 1 つ</summary>
/// <param name="handle">
/// ブラウザ側の購読
/// <c>dispose</c> を持つ
/// </param>
/// <param name="callback">
/// ブラウザ側から呼び返す入れ物
/// 呼び返さない購読では null
/// </param>
/// <remarks>閉じると、購読をやめて、ブラウザ側と .NET 側の参照を捨てる。</remarks>
internal sealed class JsSubscription(IJSObjectReference handle, JsCallback? callback = null) : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await handle.InvokeVoidAsync("dispose");
            await handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // 画面が閉じたあとは、ブラウザ側の購読も無い
        }
        callback?.Dispose();
    }
}
