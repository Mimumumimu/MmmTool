using Microsoft.Extensions.DependencyInjection;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Messages;
using MmmTool.Data.SqlServer.Notes;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Data.SqlServer.WorkItems;
using MmmTool.Data.Users;
using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Reminders.Core;
using MmmTool.Reminders.Core.Json;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer;

/// <summary>SQL Server に保存するための部品を DI に登録する</summary>
public static class SqlServerDataServiceCollectionExtensions
{
    /// <summary>SQL Server の部品を登録し、保存先の選択を設定どおりにする</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 機能が登録した JSON の保存先 (<see cref="JsonReminderRepository"/>)を、設定 (<see cref="DatabaseMode"/>)が DB のときだけ、SQL Server の保存先に置き換える。
    /// 保存先は最初に使うときに選ぶので、設定が無い初回の選択画面はその前に出せる。
    /// DB に接続するのは DB を選んだときに、最初に読み書きするとき。
    /// 機能 (<c>AddFeaturePlugin</c>)を登録したあとに呼ぶ。
    /// あとの登録が前の登録を置き換えるため。
    /// </remarks>
    public static IServiceCollection AddSqlServerData(this IServiceCollection services)
    {
        services.AddUsers();
        services.AddDatabaseSettings();

        services.AddSingleton<SqlServerConnectionFactoryBuilder>();
        services.AddSingleton<SqlServerDatabase>();
        services.AddSingleton<IAppUserRepository, SqlServerAppUserRepository>();
        services.AddSingleton<ISavedCredentialStore, SecretSavedCredentialStore>();
        services.AddSingleton<SqlServerReminderRepository>();

        services.AddStorage<IReminderRepository, JsonReminderRepository, SqlServerReminderRepository>();

        // 作業管理。保存先の選択 (ローカルの SQLite / SQL Server)は、SQLite を参照できる MmmTool.Data.Storage が、設定で行う (MmmBatch に SQLite を持ち込まないため)
        services.AddSingleton<SqlServerWorkItemRepository>();

        // ノート。保存先の選択 (ローカルの SQLite / SQL Server)は、作業管理と同じく、MmmTool.Data.Storage が、設定で行う
        services.AddSingleton<SqlServerNoteRepository>();

        // 送信先とリマインダーの送信設定は、DB モードだけで使う (ローカルモードは、機能が登録した「使えない」保存先のまま)
        services.AddSingleton<SqlServerNotificationChannelRepository>();
        services.AddSingleton<SqlServerReminderSendSettingRepository>();
        services.AddStorage<INotificationChannelRepository, UnavailableNotificationChannelRepository, SqlServerNotificationChannelRepository>();
        services.AddStorage<IReminderSendSettingRepository, UnavailableReminderSendSettingRepository, SqlServerReminderSendSettingRepository>();

        // 会話とメッセージの読み書きも、DB モードだけ。ローカルモードは、機能が登録した「使えない」保存先のまま
        services.AddSingleton<SqlServerMessageRepository>();
        services.AddStorage<IMessageRepository, UnavailableMessageRepository, SqlServerMessageRepository>();

        // ユーザーごとの設定 (メッセージの通知先など)も、DB モードだけ
        services.AddSingleton<SqlServerAppUserSettingRepository>();
        services.AddStorage<IAppUserSettingRepository, UnavailableAppUserSettingRepository, SqlServerAppUserSettingRepository>();

        // 管理者が決める設定 (機能の利用)も、DB モードだけ
        services.AddSingleton<SqlServerAppUserAdminSettingRepository>();
        services.AddStorage<IAppUserAdminSettingRepository, UnavailableAppUserAdminSettingRepository, SqlServerAppUserAdminSettingRepository>();
        return services;
    }
}
