using Microsoft.JSInterop;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// ブラウザ側から、.NET の処理を 1 つ呼び返すための入れ物。更新の知らせや、先頭に近づいたことを受けるときに使う。
/// </summary>
internal sealed class JsCallback : IDisposable
{
    /// <summary>呼ばれたときの処理。</summary>
    private readonly Func<Task> _action;

    /// <summary>
    /// 入れ物を作る。
    /// </summary>
    /// <param name="action">呼ばれたときの処理。</param>
    public JsCallback(Func<Task> action)
    {
        _action = action;
        Reference = DotNetObjectReference.Create(this);
    }

    /// <summary>ブラウザ側に渡す参照。</summary>
    public DotNetObjectReference<JsCallback> Reference { get; }

    /// <summary>
    /// ブラウザ側から呼ばれる。
    /// </summary>
    /// <returns>処理の完了を表すタスク。</returns>
    [JSInvokable]
    public Task Invoke() => _action();

    /// <inheritdoc />
    public void Dispose() => Reference.Dispose();
}
