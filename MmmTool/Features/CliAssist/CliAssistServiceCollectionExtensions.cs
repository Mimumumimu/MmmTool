using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Attachments;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Terminal;
using MmmTool.Core.CliAssist;
using MmmTool.Core.CliAssist.Json;
using MmmTool.Features.CliAssist.Main;
using MmmTool.Features.CliAssist.Setup;
using MmmTool.Features.CliAssist.WorkingDirectory;
using MmmTool.Shell;

namespace MmmTool.Features.CliAssist;

/// <summary>CLI補助の DI 登録</summary>
public static class CliAssistServiceCollectionExtensions
{
    /// <summary>CLI補助を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>保存先（定型コマンド・利用状態）はローカル専用の JSON。</remarks>
    public static IServiceCollection AddCliAssist(this IServiceCollection services)
    {
        // 保存先
        // 既定の定型コマンドはアプリが決める（補助スクリプトの配置を知っているため）。環境と使うツールは、作るときにユーザーが選ぶ（初期設定のダイアログ）
        services.AddSingleton<ICliCommandRepository>(provider => new JsonCliCommandRepository(
            provider.GetRequiredService<IJsonFileStore>(),
            async _ => CliCommandDefaults.Create(await provider.GetRequiredService<ICliSetupDialogService>().ShowFirstRunAsync())));
        services.AddSingleton<ICliSettingsRepository, JsonCliSettingsRepository>();

        services.AddSingleton<CliSettingsService>();
        // 添付の一時保存先。環境ごとに 1 つ。使うのは、定型コマンドで決まった環境のほうだけ
        // Windows: %TEMP%\MmmTool\session_日時\。終了時（Host の破棄時）に削除する
        services.AddKeyedSingleton(CliEnvironment.Windows, (provider, _) => new AttachmentStore(AppInfo.Name, provider.GetRequiredService<TimeProvider>()));
        // WSL: /tmp/MmmTool/session_日時/。終了時には削除しない（WSL の再起動で空になるのに任せる）
        services.AddKeyedSingleton(CliEnvironment.Wsl, (provider, _) => AttachmentStore.ForWsl(AppInfo.Name, provider.GetRequiredService<TimeProvider>()));
        // セッションは利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();
        services.AddSingleton<IWorkingDirectoryDialogService, WorkingDirectoryDialogService>();
        services.AddSingleton<ICliSetupDialogService, CliSetupDialogService>();

        services.AddTransient<WorkingDirectoryDialog>();
        services.AddTransient<WorkingDirectoryDialogViewModel>();
        services.AddTransient<CliSetupDialog>();
        services.AddTransient<CliSetupDialogViewModel>();
        services.AddTransient<CliAssistViewModel>();
        // 設定でオン・オフできる機能。オフの間は、ページ・起動時の準備を使わない
        services.AddFeature(CliAssistFeature.Key, CliAssistFeature.DisplayName);
        services.AddSingleton<IFeatureDisableConfirmation, CliAssistDisableConfirmation>();
        services.AddNavigationPage<CliAssistPage>(CliAssistFeature.DisplayName, "", NavigationArea.Top, CliAssistFeature.Key);

        services.AddStartupTask<CliAssistStartup>(CliAssistFeature.Key);
        return services;
    }
}
