using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.ClipboardTransfer.Core;
using MmmTool.ClipboardTransfer.Main;

namespace MmmTool.ClipboardTransfer;

/// <summary>クリップボード転送の入口 (ページを登録する)</summary>
public sealed class ClipboardTransferPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>保存するデータは無い。設定ページでオン・オフできる (オフの間はサイドバーに出さない)。</remarks>
    public void Register(IServiceCollection services)
    {
        services.AddFeature(ClipboardTransferFeature.Key, ClipboardTransferFeature.DisplayName);

        // 状態を持たない変換・保存の処理
        services.AddSingleton<ClipboardTransferService>();

        services.AddTransient<ClipboardTransferViewModel>();
        services.AddNavigationPage<ClipboardTransferPage>(ClipboardTransferFeature.DisplayName, "", NavigationArea.Top, ClipboardTransferFeature.Key);
    }
}
