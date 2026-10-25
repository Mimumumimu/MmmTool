using MmmSdk.Core.Components.Features;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>
/// 起動時に、Web 版通知の送り手の鍵を用意しておく。
/// </summary>
/// <param name="keys">送り手の鍵</param>
/// <remarks>
/// 端末の登録には、Web 版のサーバーに公開鍵を設定しておく必要があり、最初の送信より前に要る。
/// そのため、送るときではなく、起動時に作る。
/// </remarks>
public sealed class WebPushKeyStartup(WebPushKeyStore keys) : IStartupTask
{
    /// <inheritdoc />
    public async Task StartAsync()
    {
        try
        {
            await keys.GetOrCreateAsync().ConfigureAwait(false);
        }
        catch (SendFailedException)
        {
            // 起動を止めない。鍵を読めない・書けない状態は、Web 版通知を送るときに、同じ失敗として報告される
        }
    }
}
