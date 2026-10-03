using Microsoft.Extensions.DependencyInjection;
using MmmTool.Core.CliAssist;
using MmmTool.Core.CliAssist.Json;
using MmmTool.Features.CliAssist.Terminal;
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
        services.AddSingleton<ICliCommandRepository, JsonCliCommandRepository>();
        services.AddSingleton<ICliSettingsRepository, JsonCliSettingsRepository>();

        services.AddSingleton<CliSettingsService>();
        // 終了時（Host の破棄時）に添付の一時フォルダを削除する
        services.AddSingleton<AttachmentStore>();
        // セッションは利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
        services.AddTransient<ITerminalSession, PseudoConsoleSession>();
        services.AddSingleton<IImageConverter, ImageConverter>();
        services.AddSingleton<IWorkingDirectoryDialogService, WorkingDirectoryDialogService>();

        services.AddTransient<WorkingDirectoryDialog>();
        services.AddTransient<WorkingDirectoryDialogViewModel>();
        services.AddTransient<CliAssistViewModel>();
        services.AddNavigationPage<CliAssistPage>("CLI補助", "", NavigationArea.Top);

        services.AddStartupTask<CliAssistStartup>();
        return services;
    }
}
