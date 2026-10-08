using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Attachments;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Terminal;
using MmmTool.CliAssist.Core;
using MmmTool.CliAssist.Core.Json;
using MmmTool.CliAssist.Main;
using MmmTool.CliAssist.Setup;
using MmmTool.CliAssist.WorkingDirectory;

namespace MmmTool.CliAssist;

/// <summary>CLI補助の入口</summary>
/// <remarks>保存先 (定型コマンド・利用状態)はローカル専用の JSON。</remarks>
public sealed class CliAssistPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // 保存先
        // 既定の定型コマンドは Core の CliCommandDefaults が作る。環境と使うツールは、作るときにユーザーが選ぶ (初期設定のダイアログ。画面なのでアプリが出す)
        services.AddSingleton<ICliCommandRepository>(provider => new JsonCliCommandRepository(
            provider.GetRequiredService<IJsonFileStore>(),
            async _ => CliCommandDefaults.Create(await provider.GetRequiredService<ICliSetupDialogService>().ShowFirstRunAsync())));
        services.AddSingleton<ICliSettingsRepository, JsonCliSettingsRepository>();

        services.AddSingleton<CliSettingsService>();
        // 添付の一時保存先。セッション (タブ)ごと、環境ごとに 1 つ。使うのは、定型コマンドで決まった環境のほうだけ
        // Windows: %TEMP%\MmmTool\session_日時\。タブを閉じるとき・ページの破棄時に削除する
        services.AddKeyedTransient(CliEnvironment.Windows, (provider, _) => new AttachmentStore(provider.GetRequiredService<AppEnvironment>().Name, provider.GetRequiredService<TimeProvider>()));
        // WSL: /tmp/MmmTool/session_日時/。削除しない (WSL の再起動で空になるのに任せる)
        services.AddKeyedTransient(CliEnvironment.Wsl, (provider, _) => AttachmentStore.ForWsl(provider.GetRequiredService<AppEnvironment>().Name, provider.GetRequiredService<TimeProvider>()));
        // ターミナルのセッションは利用側ごとに 1 つ。作ったスコープ (ページ)の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();
        services.AddSingleton<IWorkingDirectoryDialogService, WorkingDirectoryDialogService>();
        services.AddSingleton<ICliSetupDialogService, CliSetupDialogService>();

        services.AddTransient<WorkingDirectoryDialog>();
        services.AddTransient<WorkingDirectoryDialogViewModel>();
        services.AddTransient<CliSetupDialog>();
        services.AddTransient<CliSetupDialogViewModel>();
        // 送信履歴はページのスコープに 1 つ (セッションをまたいで共通。ページを捨てると一緒に消える)
        services.AddScoped<SendHistory>();
        services.AddScoped<CliSessionFactory>();
        services.AddTransient<CliSessionViewModel>();
        services.AddTransient<CliAssistViewModel>();
        // 設定でオン・オフできる機能。オフの間は、ページ・起動時の準備を使わない
        services.AddFeature(CliAssistFeature.Key, CliAssistFeature.DisplayName, defaultEnabled: false);
        services.AddSingleton<IFeatureDisableConfirmation, CliAssistDisableConfirmation>();
        services.AddNavigationPage<CliAssistPage>(CliAssistFeature.DisplayName, "", NavigationArea.Top, CliAssistFeature.Key);

        services.AddStartupTask<CliAssistStartup>(CliAssistFeature.Key);
    }
}
