using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Dialogs;

namespace MmmTool.Features.CliAssist.WorkingDirectory;

/// <summary>作業ディレクトリ変更ダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>ダイアログは開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public sealed class WorkingDirectoryDialogService(IServiceProvider services, IDialogHost dialogs) : IWorkingDirectoryDialogService
{
    /// <inheritdoc />
    public Task<string?> ShowAsync()
    {
        var dialog = services.GetRequiredService<WorkingDirectoryDialog>();
        dialog.XamlRoot = dialogs.Owner.Content.XamlRoot;
        return dialog.PickAsync();
    }
}
