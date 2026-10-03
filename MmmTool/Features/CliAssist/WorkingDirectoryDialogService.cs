using Microsoft.Extensions.DependencyInjection;
using MmmTool.Services;

namespace MmmTool.Features.CliAssist;

/// <summary>作業ディレクトリ変更ダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>ダイアログは開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public sealed class WorkingDirectoryDialogService(IServiceProvider services, DialogService dialogs) : IWorkingDirectoryDialogService
{
    /// <inheritdoc />
    public Task<string?> ShowAsync()
    {
        var dialog = services.GetRequiredService<WorkingDirectoryDialog>();
        dialog.XamlRoot = dialogs.Owner.Content.XamlRoot;
        return dialog.PickAsync();
    }
}
