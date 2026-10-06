using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CliAssist.Main;

namespace MmmTool.CliAssist;

/// <summary>CLI補助をオフにする前の確認 (どれかのタブでターミナルが動いているときだけ)</summary>
/// <param name="services">作ったページを引くための DI のサービスプロバイダー (ターミナルは、ページの ViewModel が持つ)</param>
/// <param name="dialogs">確認ダイアログ</param>
/// <remarks>
/// ページをまだ開いていない・シェルが始まっていない・すでに終わっているときは、失うものが無いので、確認しない。
/// ページを持つホスト側の <see cref="IPageCache"/> は、機能のオン・オフの管理に依存し、その管理はこの確認に依存するので、循環しないよう、使うときに取り出す。
/// </remarks>
public sealed class CliAssistDisableConfirmation(IServiceProvider services, IDialogService dialogs) : IFeatureDisableConfirmation
{
    /// <inheritdoc />
    public string FeatureKey => CliAssistFeature.Key;

    /// <inheritdoc />
    public Task<bool> ConfirmAsync()
    {
        var pages = services.GetRequiredService<IPageCache>();
        if (pages.GetCreatedPage<CliAssistPage>() is not { } page
            || !page.ViewModel.Sessions.Any(session => session.Terminal is { IsStarted: true, HasExited: false }))
        {
            return Task.FromResult(true);
        }

        return dialogs.ConfirmAsync(
            "CLI補助をオフにしますか？",
            "すべてのタブの実行中のターミナルと、その中の作業が終了します。",
            "オフにする",
            "オンのままにする");
    }
}
