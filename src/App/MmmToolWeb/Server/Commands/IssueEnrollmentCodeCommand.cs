using MmmSdk.Core.Components.Storage;
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Auth.Core.Enrollment;
using MmmToolWeb.Server.Hosting;

namespace MmmToolWeb.Server.Commands;

/// <summary>
/// サーバーの PC で行う、登録用コードの発行 (<c>issue-code ログイン名</c>)。その人の端末でパスキーを作るときに入力するコードを出す。
/// </summary>
internal static class IssueEnrollmentCodeCommand
{
    /// <summary>コマンドの名前。</summary>
    public const string Name = "issue-code";

    /// <summary>
    /// コードを発行して、画面に出す。
    /// </summary>
    /// <param name="services">サービスの入口。</param>
    /// <param name="loginName">コードを出す人のログイン名。</param>
    /// <returns>終了コード。発行したら 0、できなかったら 1。</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string loginName)
    {
        try
        {
            var user = await services.GetRequiredService<WebUserLookup>().FindActiveByLoginNameAsync(loginName, CancellationToken.None);
            if (user is null)
            {
                Console.WriteLine($"ログイン名「{loginName}」の、今使えるユーザーが見つかりません。");
                return 1;
            }

            var issued = await services.GetRequiredService<EnrollmentCodeService>().IssueAsync(user.Id, CancellationToken.None);
            services.GetRequiredService<ILoggerFactory>().CreateLogger(SecurityLog.Category)
                .LogInformation("登録用コードを発行しました。ユーザー番号={UserId}", user.Id);
            Console.WriteLine($"{user.DisplayName} さんの登録用コード: {issued.Code}");
            Console.WriteLine($"期限: {issued.ExpiresAt.ToLocalTime():yyyy-MM-dd HH:mm} まで (1 回だけ使えます。前に出したコードは使えなくなります)");
            return 0;
        }
        catch (Exception ex) when (ex is DataFileException or InvalidOperationException)
        {
            // DB の設定が足りない・接続できない・表が無いときなど。管理者に、画面に出せるメッセージを見せる
            Console.WriteLine(ex.Message);
            return 1;
        }
    }
}
