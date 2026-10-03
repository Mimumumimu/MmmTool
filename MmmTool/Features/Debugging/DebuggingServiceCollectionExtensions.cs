using Microsoft.Extensions.DependencyInjection;
using MmmTool.Shell;

namespace MmmTool.Features.Debugging;

/// <summary>DEBUG ページの DI 登録</summary>
public static class DebuggingServiceCollectionExtensions
{
    /// <summary>DEBUG ページを登録する（デバッグビルドのときだけ）</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>リリースビルドでは何も登録しない（サイドバーに DEBUG が出ない）。</remarks>
    public static IServiceCollection AddDebugging(this IServiceCollection services)
    {
#if DEBUG
        services.AddTransient<DebugViewModel>();
        services.AddNavigationPage<DebugPage>("DEBUG", "", NavigationArea.Footer);
#endif
        return services;
    }
}
