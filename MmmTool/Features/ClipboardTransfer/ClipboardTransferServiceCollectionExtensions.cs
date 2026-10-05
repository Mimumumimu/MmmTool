using Microsoft.Extensions.DependencyInjection;
using MmmTool.Core.ClipboardTransfer;
using MmmTool.Features.ClipboardTransfer.Main;
using MmmTool.Shell;

namespace MmmTool.Features.ClipboardTransfer;

/// <summary>クリップボード転送の DI 登録</summary>
public static class ClipboardTransferServiceCollectionExtensions
{
    /// <summary>クリップボード転送 (ページ)を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>保存するデータは無い。設定ページでオン・オフできる (オフの間はサイドバーに出さない)。</remarks>
    public static IServiceCollection AddClipboardTransfer(this IServiceCollection services)
    {
        services.AddFeature(ClipboardTransferFeature.Key, ClipboardTransferFeature.DisplayName);

        // 状態を持たない変換・保存の処理
        services.AddSingleton<ClipboardTransferService>();

        services.AddTransient<ClipboardTransferViewModel>();
        services.AddNavigationPage<ClipboardTransferPage>(ClipboardTransferFeature.DisplayName, "", NavigationArea.Top, ClipboardTransferFeature.Key);
        return services;
    }
}
