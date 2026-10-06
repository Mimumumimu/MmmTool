using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.Setup;

/// <summary>CLI補助の初期設定ダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>ダイアログは開くたびに DI から作る。</remarks>
public sealed class CliSetupDialogService(IServiceProvider services, IDialogHost dialogs) : ICliSetupDialogService
{
    /// <inheritdoc />
    public Task<CliSetup> ShowFirstRunAsync() => CreateDialog().ShowFirstRunAsync();

    /// <inheritdoc />
    public Task<CliSetup?> ShowResetAsync(CliEnvironment currentEnvironment) => CreateDialog().ShowResetAsync(currentEnvironment);

    /// <summary>ダイアログを作り、親のウィンドウに載せる</summary>
    /// <returns>開く前のダイアログ</returns>
    private CliSetupDialog CreateDialog()
    {
        var dialog = services.GetRequiredService<CliSetupDialog>();
        dialog.XamlRoot = dialogs.Owner.Content.XamlRoot;
        return dialog;
    }
}
