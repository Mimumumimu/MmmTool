using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// ブラウザの今日の日付と、アプリが前面に戻ったことの通知。
/// </summary>
/// <param name="js">JavaScript の呼び出し。</param>
/// <remarks>
/// 今日の日付は、ブラウザ (iPhone)の時計で決める。ブラウザ上の .NET は、端末のタイムゾーンを知らないため。
/// ホーム画面のアプリは、裏に回ると止まるので、他の人の更新を取り込む読み直しは、前面に戻ったときに行う。
/// </remarks>
public sealed class BrowserClient(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>ブラウザ側の部品 <c>js/browser.js</c>。</summary>
    private IJSObjectReference? _module;

    /// <summary>前面に戻ったことの購読。</summary>
    private IJSObjectReference? _subscription;

    /// <summary>JavaScript から呼ばれる側の入口。</summary>
    private DotNetObjectReference<BrowserClient>? _self;

    /// <summary>アプリが、裏から前面に戻った。</summary>
    public event Func<Task>? BecameVisible;

    /// <summary>
    /// ブラウザの時計の今日を返す。
    /// </summary>
    /// <returns>今日の日付。</returns>
    public async Task<DateOnly> GetTodayAsync()
    {
        var module = await GetModuleAsync();
        return DateOnly.ParseExact(await module.InvokeAsync<string>("todayLocal"), "yyyy-MM-dd");
    }

    /// <summary>
    /// この端末で覚えている値を返す。
    /// </summary>
    /// <param name="key">値の名前。機能どうしで重ならないよう、機能の名前から始める。</param>
    /// <returns>覚えている値。まだ無いとき、読めないときは null。</returns>
    /// <remarks>端末ごとの使い勝手の記憶で、ブラウザの <c>localStorage</c> に置く。サーバーには送らない。</remarks>
    public async Task<string?> GetRememberedAsync(string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getRemembered", key);
    }

    /// <summary>
    /// この端末に値を覚える。覚えられなくても、エラーにしない。
    /// </summary>
    /// <param name="key">値の名前。機能どうしで重ならないよう、機能の名前から始める。</param>
    /// <param name="value">覚える値。</param>
    /// <returns>覚えたことを表すタスク。</returns>
    public async Task RememberAsync(string key, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setRemembered", key, value);
    }

    /// <summary>
    /// 端末の呼び名を返す。
    /// </summary>
    /// <returns>iPhone と iPad は「iPhone」、それ以外は「ブラウザ」。</returns>
    public async Task<string> GetDeviceNameAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("deviceName");
    }

    /// <summary>
    /// このブラウザで通知を受け取れる状態かを返す。サービスワーカーも登録しておく。
    /// </summary>
    /// <returns>通知の状態。</returns>
    public async Task<PushStatus> GetPushStatusAsync()
    {
        var module = await GetModuleAsync();
        var result = await module.InvokeAsync<JsonElement>("getPushStatus");
        var state = result.GetProperty("state").GetString() switch
        {
            "ready" => PushState.Ready,
            "denied" => PushState.Denied,
            "needs-home-screen" => PushState.NeedsHomeScreen,
            _ => PushState.Unsupported,
        };
        var endpoint = result.TryGetProperty("endpoint", out var endpointValue) ? endpointValue.GetString() : null;
        return new PushStatus(state, result.GetProperty("subscribed").GetBoolean(), endpoint);
    }

    /// <summary>
    /// 通知の許可を求めて、このブラウザの宛先を作る。
    /// </summary>
    /// <param name="publicKey">サーバーが返した、送り手の公開鍵で、base64url の文字列。</param>
    /// <returns>宛先。許可されなかったときは null。</returns>
    /// <remarks>押した操作の中で、ほかの待ちより先に呼ぶ。iPhone は、押した直後でないと許可を求められない。</remarks>
    public async Task<PushSubscriptionInfo?> SubscribePushAsync(string publicKey)
    {
        var module = await GetModuleAsync();
        var result = await module.InvokeAsync<JsonElement>("subscribePush", publicKey);
        return result.GetProperty("status").GetString() == "granted"
            ? new PushSubscriptionInfo(
                result.GetProperty("endpoint").GetString() ?? "",
                result.GetProperty("p256dh").GetString() ?? "",
                result.GetProperty("auth").GetString() ?? "")
            : null;
    }

    /// <summary>
    /// このブラウザの宛先を捨てる。無ければ何もしない。
    /// </summary>
    /// <returns>捨てたことを表すタスク。</returns>
    public async Task UnsubscribePushAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unsubscribePush");
    }

    /// <summary>
    /// 前面に戻ったことの通知を始める。
    /// </summary>
    /// <returns>始めたことを表すタスク。</returns>
    public async Task StartWatchingVisibilityAsync()
    {
        if (_subscription is not null)
        {
            return;
        }
        var module = await GetModuleAsync();
        _self = DotNetObjectReference.Create(this);
        _subscription = await module.InvokeAsync<IJSObjectReference>("watchVisibility", _self);
    }

    /// <summary>
    /// 画面がいま前面に見えているかを返す。
    /// </summary>
    /// <returns>前面に見えているとき true。裏に回っている・画面が消えているときは false。</returns>
    public async Task<bool> IsVisibleAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isVisible");
    }

    /// <summary>
    /// 端末の控えの値を返す。
    /// </summary>
    /// <param name="key">値の名前。機能どうしで重ならないよう、機能の名前から始める。</param>
    /// <returns>控えの値。無いとき、読めないときは null。</returns>
    /// <remarks>控えは、ブラウザの IndexedDB に置く。サーバーが唯一の正で、控えはいつでも作り直せる。サーバーには送らない。</remarks>
    public async Task<string?> CacheGetAsync(string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("cacheGet", key);
    }

    /// <summary>
    /// 端末の控えに値を置く。置けなくても、エラーにしない。
    /// </summary>
    /// <param name="key">値の名前。機能どうしで重ならないよう、機能の名前から始める。</param>
    /// <param name="value">置く値。</param>
    /// <returns>置いたことを表すタスク。</returns>
    public async Task CachePutAsync(string key, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("cachePut", key, value);
    }

    /// <summary>
    /// 端末の控えを全部捨てる。ログアウトとアカウントの変更で呼ぶ。
    /// </summary>
    /// <returns>捨てたことを表すタスク。</returns>
    public async Task ClearCacheAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("cacheClear");
    }

    /// <summary>
    /// 読んでいない通知の識別子を返す。
    /// </summary>
    /// <returns>種類:番号 の形の識別子。無いとき、読めないときは空。</returns>
    /// <remarks>一覧は端末の控えに置き、ホーム画面のアイコンの数字は、この件数に合わせる。ログアウトとアカウントの変更で、控えと一緒に消える。</remarks>
    public async Task<IReadOnlyList<string>> GetUnreadAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string[]>("unreadGet");
    }

    /// <summary>
    /// サーバーが数える種類の、読んでいない件数を置いて、ホーム画面のアイコンの数字を直す。
    /// </summary>
    /// <param name="kind">通知の種類。</param>
    /// <param name="count">サーバーから読んだ、読んでいない件数。</param>
    /// <returns>置いたことを表すタスク。</returns>
    /// <remarks>メッセージは、読んだ位置を DB に持つので、端末では数えず、サーバーから読んだ件数を置く。</remarks>
    public async Task SetServerUnreadAsync(string kind, int count)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unreadSetServer", kind, count);
    }

    /// <summary>
    /// 種類の通知を、すべて読んだことにする。
    /// </summary>
    /// <param name="kind">通知の種類。</param>
    /// <returns>外したことを表すタスク。</returns>
    public async Task ClearUnreadAsync(string kind)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unreadClear", kind);
    }

    /// <summary>
    /// サーバーからの更新の知らせ (Server-Sent Events)を受け始める。
    /// </summary>
    /// <param name="url">知らせの道。</param>
    /// <param name="onChanged">「changed」の知らせを受けたときの処理。</param>
    /// <returns>閉じると、つなぎをやめる購読。</returns>
    public async Task<IAsyncDisposable> OpenEventsAsync(string url, Func<Task> onChanged)
    {
        var module = await GetModuleAsync();
        var callback = new JsCallback(onChanged);
        var handle = await module.InvokeAsync<IJSObjectReference>("openEvents", url, callback.Reference);
        return new JsSubscription(handle, callback);
    }

    /// <summary>
    /// 要素が先頭に近づいたことを、受け始める。
    /// </summary>
    /// <param name="element">スクロールする要素。</param>
    /// <param name="onNearTop">先頭に近づいたときの処理。</param>
    /// <returns>閉じると、やめる購読。</returns>
    public async Task<IAsyncDisposable> WatchScrollTopAsync(ElementReference element, Func<Task> onNearTop)
    {
        var module = await GetModuleAsync();
        var callback = new JsCallback(onNearTop);
        var handle = await module.InvokeAsync<IJSObjectReference>("watchScrollTop", element, callback.Reference);
        return new JsSubscription(handle, callback);
    }

    /// <summary>
    /// 全画面のシートを、キーボードを除いた見える範囲に合わせ始める。キーボードを閉じたあとの、画面のずれも戻す。
    /// </summary>
    /// <returns>閉じると、やめる購読。</returns>
    public async Task<IAsyncDisposable> WatchSheetViewportAsync()
    {
        var module = await GetModuleAsync();
        var handle = await module.InvokeAsync<IJSObjectReference>("watchSheetViewport");
        return new JsSubscription(handle);
    }

    /// <summary>
    /// 要素の中身の高さを返す。古いメッセージを足す前に測る。
    /// </summary>
    /// <param name="element">スクロールする要素。</param>
    /// <returns>中身の高さ。</returns>
    public async Task<double> GetScrollHeightAsync(ElementReference element)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<double>("getScrollHeight", element);
    }

    /// <summary>
    /// 古いメッセージを上に足したあと、見ていた位置を保つ。
    /// </summary>
    /// <param name="element">スクロールする要素。</param>
    /// <param name="previousHeight">足す前の中身の高さ。</param>
    /// <returns>位置を直したことを表すタスク。</returns>
    public async Task KeepScrollAfterPrependAsync(ElementReference element, double previousHeight)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("keepScrollAfterPrepend", element, previousHeight);
    }

    /// <summary>
    /// 要素を、一番下までスクロールする。
    /// </summary>
    /// <param name="element">スクロールする要素。</param>
    /// <returns>スクロールしたことを表すタスク。</returns>
    public async Task ScrollToBottomAsync(ElementReference element)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("scrollToBottom", element);
    }

    /// <summary>
    /// 要素が、ほぼ一番下を見ているかを返す。
    /// </summary>
    /// <param name="element">スクロールする要素。</param>
    /// <returns>ほぼ一番下を見ているとき true。</returns>
    public async Task<bool> IsNearBottomAsync(ElementReference element)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isNearBottom", element);
    }

    /// <summary>
    /// id の要素を、画面の中ほどに出す。
    /// </summary>
    /// <param name="id">要素の id。</param>
    /// <returns>スクロールしたことを表すタスク。</returns>
    public async Task ScrollIntoViewAsync(string id)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("scrollIntoViewById", id);
    }

    /// <summary>
    /// ブラウザ側から呼ばれる。前面に戻ったことを知らせる。
    /// </summary>
    /// <returns>知らせ終えたことを表すタスク。</returns>
    [JSInvokable]
    public Task NotifyVisible() => BecameVisible?.Invoke() ?? Task.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_subscription is not null)
        {
            await _subscription.InvokeVoidAsync("dispose");
            await _subscription.DisposeAsync();
        }
        _self?.Dispose();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    /// <summary>
    /// ブラウザ側の部品を、最初の 1 回だけ読み込む。
    /// </summary>
    /// <returns>読み込んだ部品。</returns>
    private async ValueTask<IJSObjectReference> GetModuleAsync()
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/browser.js");
}
