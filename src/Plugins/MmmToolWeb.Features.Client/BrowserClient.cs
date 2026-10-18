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
    /// 入力欄のカーソルを先頭に置く。
    /// </summary>
    /// <param name="element">入力欄。</param>
    /// <returns>置いたことを表すタスク。</returns>
    public async Task PlaceCaretAtStartAsync(ElementReference element)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("placeCaretAtStart", element);
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
