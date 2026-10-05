using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.Features.CliAssist.Main;
using MmmTool.Shell;
using MmmTool.Shell.Main;

namespace MmmTool.Features.CliAssist;

/// <summary>CLI補助をオフにする前の確認（ターミナルが動いているときだけ）</summary>
/// <param name="services">作ったページを引くための DI のサービスプロバイダー（ターミナルは、ページの ViewModel が持つ）</param>
/// <param name="dialogs">確認ダイアログ</param>
/// <remarks>
/// ページをまだ開いていない・シェルが始まっていない・すでに終わっているときは、失うものが無いので、確認しない。
/// <see cref="PageProvider"/> は <see cref="FeatureService"/> に依存し、<see cref="FeatureService"/> はこの確認に依存するので、循環しないよう、使うときに取り出す。
/// </remarks>
public sealed class CliAssistDisableConfirmation(IServiceProvider services, IDialogService dialogs) : IFeatureDisableConfirmation
{
    /// <inheritdoc />
    public string FeatureKey => CliAssistFeature.Key;

    /// <inheritdoc />
    public Task<bool> ConfirmAsync()
    {
        var pages = services.GetRequiredService<PageProvider>();
        if (pages.GetCreatedPage<CliAssistPage>() is not { ViewModel.Terminal: { IsStarted: true, HasExited: false } })
        {
            return Task.FromResult(true);
        }

        return dialogs.ConfirmAsync(
            "CLI補助をオフにしますか？",
            "実行中のターミナルと、その中の作業が終了します。",
            "オフにする",
            "オンのままにする");
    }
}
