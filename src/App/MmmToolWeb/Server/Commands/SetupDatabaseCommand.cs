using MmmSdk.Core.Components.Secrets;
using MmmSdk.Db.SqlServer.Components.Connections;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;

namespace MmmToolWeb.Server.Commands;

/// <summary>
/// サーバーの PC で行う、DB の接続の設定の入力 (<c>setup-db</c>)。サーバー名・データベース名・ユーザー名・パスワードを順に聞き、接続を試して、通ったら保存する。
/// </summary>
internal static class SetupDatabaseCommand
{
    /// <summary>コマンドの名前。</summary>
    public const string Name = "setup-db";

    /// <summary>
    /// 設定を聞いて、保存する。
    /// </summary>
    /// <param name="services">サービスの入口。</param>
    /// <returns>終了コード。保存したら 0、保存しなかったら 1。</returns>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var settings = services.GetRequiredService<DatabaseSettingsService>();
        if (settings.IsReadOnly)
        {
            Console.WriteLine("設定ファイル (Data\\AppSettings.json)を読めないため、保存できません。ファイルを確認してください。");
            return 1;
        }

        var current = settings.Load();
        Console.WriteLine("DB の接続の設定を入力します。何も入力せずに Enter を押すと、[ ] の中の値を使います。");
        var server = ConsoleInput.Ask("サーバー名", current.Server);
        var databaseName = ConsoleInput.Ask("データベース名", current.Name);
        var userName = ConsoleInput.Ask("ユーザー名", current.UserName);
        var trust = ConsoleInput.AskYesNo("サーバーの証明書を検証せずに信頼しますか (自己署名の証明書のとき)", current.TrustServerCertificate);

        string? savedPassword;
        try
        {
            savedPassword = settings.GetPassword();
        }
        catch (SecretStoreException ex)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }
        var password = ConsoleInput.AskPassword(savedPassword is not null);
        if (string.IsNullOrEmpty(password))
        {
            password = savedPassword;
        }

        var value = current with
        {
            Mode = DatabaseMode.SqlServer,
            Server = server,
            Name = databaseName,
            Authentication = DatabaseAuthentication.Sql,
            UserName = userName,
            TrustServerCertificate = trust,
        };

        try
        {
            await SqlServerConnectionFactoryBuilder.TestConnectionAsync(value, password);
        }
        catch (Exception ex) when (ex is DatabaseSettingsException or SqlServerConnectionException)
        {
            Console.WriteLine($"接続できませんでした。保存していません。{ex.Message}");
            return 1;
        }

        // パスワードを先に保存する (設定だけが新しくなって、パスワードが古いまま残る状態を作らないため)
        try
        {
            settings.SetPassword(password!);
        }
        catch (SecretStoreException ex)
        {
            Console.WriteLine($"パスワードを保存できませんでした。設定は保存していません。{ex.Message}");
            return 1;
        }
        if (!await settings.SaveAsync(value))
        {
            Console.WriteLine("設定を保存できませんでした。");
            return 1;
        }
        Console.WriteLine("接続できました。設定を保存しました。");
        return 0;
    }
}
