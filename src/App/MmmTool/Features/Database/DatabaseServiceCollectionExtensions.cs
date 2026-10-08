using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Features.Database.Choice;
using MmmTool.Features.Database.Connection;
using MmmTool.Features.Database.Edit;
using MmmTool.Features.Database.Settings;

namespace MmmTool.Features.Database;

/// <summary>
/// 保存先 (ローカル / DB)の画面 (設定ページの部品・編集画面・初回の選択の画面)を DI に登録する。
/// </summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>保存先の画面を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>接続の設定の読み書き (<c>DatabaseSettingsService</c>)は、DB の種類ごとの登録 (<c>AddSqlServerData</c>)が行う。設定ページの部品は、登録順で並ぶので、各機能の登録のあとに呼ぶ。</remarks>
    public static IServiceCollection AddDatabaseScreens(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseChoiceService>();
        services.AddTransient<DatabaseConnectionViewModel>();
        services.AddSingleton<IDatabaseDialogService, DatabaseDialogService>();

        // 設定ページに並べる部品 (保存先のカード)
        services.AddTransient<DatabaseSettingsViewModel>();
        services.AddSettingsSection<DatabaseSettingsControl>();

        // 保存先と接続の編集画面。閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<DatabaseEditWindow>();
        services.AddTransient<DatabaseEditViewModel>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<DatabaseChoiceWindow>();
        services.AddTransient<DatabaseChoiceViewModel>();
        return services;
    }
}
