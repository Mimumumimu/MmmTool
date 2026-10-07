using Microsoft.Extensions.DependencyInjection;
using MmmTool.Features.Users.Registration;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users;

/// <summary>
/// ユーザーの登録の画面を開く。アプリ全体で 1 つ。
/// </summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <remarks>
/// 起動時の準備 (<see cref="UserStartup"/>)が、登録が要ると分かったとき (<see cref="RequestRegistration"/>)に印を付け、
/// メインウィンドウを作ったあと、トレイを出す前に、<see cref="ShowIfRequestedAsync"/> が画面を出す。登録は必須で、登録しないと先へ進ませない。画面は閉じると再表示できないので、開くたびに DI から作る。UI スレッドから呼ぶ。
/// 画面 (と ViewModel)は、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため)。
/// </remarks>
public sealed class UserRegistrationService(IServiceScopeFactory scopeFactory)
{
    /// <summary>登録の画面を出す必要があるか</summary>
    private volatile bool _requested;

    /// <summary>登録の画面を出す必要がある、と印を付ける</summary>
    public void RequestRegistration() => _requested = true;

    /// <summary>印があれば、登録の画面を出して、閉じるまで待つ</summary>
    /// <returns>先へ進んでよければ true (登録した・登録が要らなかった)。登録せずに閉じたら false (アプリを終了する)</returns>
    public async Task<bool> ShowIfRequestedAsync()
    {
        if (!_requested)
        {
            return true;
        }

        _requested = false;
        return await ShowAsync() is not null;
    }

    /// <summary>登録の画面を出して、閉じるまで待つ</summary>
    /// <returns>登録したユーザー。登録せずに閉じたら null</returns>
    public async Task<AppUser?> ShowAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<UserRegistrationWindow>();
        return await window.ShowAsync();
    }
}
