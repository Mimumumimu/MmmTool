using Microsoft.Extensions.Logging;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Storage;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users;

/// <summary>
/// DB モードのとき、管理者が決めた、今のユーザーの機能の利用を読み込む起動時の準備。
/// </summary>
/// <remarks>
/// 覚えているログインで特定できたときは、メインウィンドウを作る前に読み込む。サイドバーに、無効の機能を出さないため。
/// ログインの画面で特定したときは、特定のあとに読み込んで、<see cref="FeatureService.NotifyAvailabilityChanged"/> でサイドバーに知らせる。
/// ローカルモードでは、何も読み込まず、すべて有効になる。読めなかったときは、ログに残して、すべて有効のままにする。
/// 管理者の変更は、起動のときの読み込みから反映される。
/// </remarks>
/// <param name="currentUser">今のユーザー</param>
/// <param name="cache">機能の利用の控え</param>
/// <param name="features">機能のオン・オフの管理。サイドバーへの通知に使う</param>
/// <param name="logger">ログ</param>
public sealed class FeatureAccessStartup(
    CurrentUser currentUser, FeatureAccessCache cache, FeatureService features, ILogger<FeatureAccessStartup> logger) : IStartupTask, IDisposable
{
    /// <summary>起動時の UI スレッドの同期コンテキスト。ログインの画面のあとに、サイドバーへ知らせるために使う</summary>
    private SynchronizationContext? _uiContext;

    /// <inheritdoc />
    public async Task StartAsync()
    {
        _uiContext = SynchronizationContext.Current;
        currentUser.Changed += OnCurrentUserChanged;
        await LoadAsync();
    }

    /// <summary>購読を解除する</summary>
    public void Dispose() => currentUser.Changed -= OnCurrentUserChanged;

    /// <summary>今のユーザーが特定されたら、機能の利用を読み込んで、サイドバーに知らせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>任意のスレッドから呼ばれる。サイドバーへの通知は、UI スレッドで行う。</remarks>
    private async void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        if (!await LoadAsync())
        {
            return;
        }

        if (_uiContext is { } context)
        {
            context.Post(_ => features.NotifyAvailabilityChanged(), null);
        }
    }

    /// <summary>機能の利用を読み込む</summary>
    /// <returns>読み込めたら true。読めなかったら false</returns>
    private async Task<bool> LoadAsync()
    {
        try
        {
            await cache.RefreshAsync();
            return true;
        }
        catch (DataFileException ex)
        {
            logger.LogWarning(ex, "機能の利用を読み込めませんでした。すべて有効として動きます。");
            return false;
        }
    }
}
