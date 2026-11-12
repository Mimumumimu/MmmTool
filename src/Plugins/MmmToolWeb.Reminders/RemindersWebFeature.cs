using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.NotificationChannels.Core;
using MmmToolWeb.Features;
using MmmToolWeb.Reminders.Client;

namespace MmmToolWeb.Reminders;

/// <summary>リマインダーの Web 版のサーバー側</summary>
/// <remarks>
/// 画面の API と、「通知」の画面の一覧に出す提供元と、通知を押したときの行き先を登録する。
/// 状態 (未・スヌーズ・完了)は読むだけで、書かない。
/// 変えるのは PC 版だけ。
/// 時刻に外へ送る通知は MmmBatch が行うので、この機能は通知を出さない。
/// リマインダーを Web 版の「通知」に出さないときはサーバー本体のこの機能の登録の 1 行を外す。
/// </remarks>
public sealed class RemindersWebFeature : IWebFeature
{
    /// <inheritdoc />
    public Assembly ClientAssembly => typeof(RemindersClient).Assembly;

    /// <inheritdoc />
    public int? FeatureNo => CodeFeatures.Reminders;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // 保存先と提供元は、要求ごとに作る。今のユーザーが、要求ごとに違うため
        services.AddScoped<SqlServerReminderWebRepository>();
        services.AddScoped<SqlServerReminderSendSettingRepository>();
        services.AddScoped<IReceivedNotificationSource, SqlServerReceivedReminderSource>();

        // 送信先の一覧は、「通知」の機能と同じ保存先から読む。どちらの機能の登録が先でも、1 つだけ登録する
        services.TryAddScoped<INotificationChannelRepository, SqlServerNotificationChannelRepository>();

        // リマインダーの通知を押したときに、そのリマインダーを強調して、リマインダーの画面を開く
        services.AddScoped<INotificationDestination, ReminderNotificationDestination>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, RemindersJsonContext.Default));
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api) => ReminderEndpoints.Map(api.MapGroup("/reminders"));
}
