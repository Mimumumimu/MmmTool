using Microsoft.Extensions.DependencyInjection;
using MmmTool.Features.Users.SignIn;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users;

/// <summary>ログインの画面を開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <remarks>
/// アプリ全体で 1 つ。
/// 起動時の準備 (<see cref="UserStartup"/>)がログインが要ると分かったとき (<see cref="RequestSignIn"/>)に印を付け、
/// メインウィンドウを作ったあと、トレイを出す前に、<see cref="ShowIfRequestedAsync"/> が画面を出す。
/// ログインは必須で、済ませないと先へ進ませない。
/// 画面は閉じると再表示できないので、開くたびに DI から作る。
/// UI スレッドから呼ぶ。
/// 画面 (と ViewModel)は開くたびに作るスコープから解決し、閉じたらスコープごと破棄する。
/// ルートから解決した <see cref="IDisposable"/> の Transient はアプリの終了まで DI コンテナが保持し続けるため。
/// </remarks>
public sealed class SignInPrompt(IServiceScopeFactory scopeFactory)
{
    /// <summary>ログインの画面を出す必要があるか</summary>
    private volatile bool _requested;

    /// <summary>ログインの画面を出す必要があると印を付ける</summary>
    public void RequestSignIn() => _requested = true;

    /// <summary>印があればログインの画面を出して閉じるまで待つ</summary>
    /// <returns>
    /// 先へ進んでよければ true で、ログインした・ログインが要らなかったとき。
    /// ログインせずに閉じたら false で、アプリを終了する。
    /// </returns>
    public async Task<bool> ShowIfRequestedAsync()
    {
        if (!_requested)
        {
            return true;
        }

        _requested = false;
        return await ShowAsync() is not null;
    }

    /// <summary>ログインの画面を出して閉じるまで待つ</summary>
    /// <returns>
    /// ログインしたユーザー
    /// ログインせずに閉じたら null
    /// </returns>
    public async Task<AppUser?> ShowAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<SignInWindow>();
        return await window.ShowAsync();
    }
}
