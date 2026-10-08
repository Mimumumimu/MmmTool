using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.Connection;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users;

/// <summary>
/// DB モードのとき、起動時に、覚えているログイン名とパスワードで、今のユーザーを特定する (起動時の準備)。
/// </summary>
/// <remarks>
/// ローカルモードでは、何もしない (DB に接続しない)。各機能の準備より先に動く (機能が DB の保存先を使う前に、今のユーザーを決めておくため)。
/// 覚えていない・合わないとき (初回など)は、ログインの画面を出す印を付ける (画面は、メインウィンドウを作ったあと、トレイを出す前に出し、ログインを済ませないと先へ進ませない。<see cref="SignInPrompt.ShowIfRequestedAsync"/>)。
/// 接続できない・設定が足りないときは、ここでは知らせず、起動を止めない。保存先 (リマインダーなど)が、最初に読むときに、同じ理由を読み込みの失敗として知らせる。
/// </remarks>
/// <param name="settings">DB への接続の設定</param>
/// <param name="signIn">ログイン</param>
/// <param name="prompt">ログインの画面</param>
/// <param name="time">現在の日付を知るための時計</param>
public sealed class UserStartup(
    DatabaseSettingsService settings, UserSignInService signIn, SignInPrompt prompt, TimeProvider time) : IStartupTask
{
    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (settings.Load().Mode != DatabaseMode.SqlServer)
        {
            return;
        }

        try
        {
            if (await signIn.TrySignInWithSavedAsync(DateOnly.FromDateTime(time.GetLocalNow().DateTime)) is null)
            {
                prompt.RequestSignIn();
            }
        }
        catch (DataFileException)
        {
            // 接続できない・設定が足りない。保存先が、読み込みの失敗として知らせる
        }
    }
}
