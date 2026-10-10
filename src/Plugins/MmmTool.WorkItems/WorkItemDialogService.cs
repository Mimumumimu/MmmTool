using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.WorkItems.Move;
using MmmTool.WorkItems.ProgressLabels;

namespace MmmTool.WorkItems;

/// <summary>作業リストのダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>ダイアログは開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public sealed class WorkItemDialogService(IServiceProvider services, IDialogHost dialogs) : IWorkItemDialogService
{
    /// <inheritdoc />
    public Task<int?> PickDestinationAsync(IReadOnlyList<MoveDestination> destinations)
    {
        var dialog = services.GetRequiredService<MoveDialog>();
        dialogs.Attach(dialog);
        return dialog.PickAsync(destinations);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, string>?> EditProgressLabelsAsync(IReadOnlyDictionary<int, string> labels)
    {
        var dialog = services.GetRequiredService<ProgressLabelsDialog>();
        dialogs.Attach(dialog);
        return dialog.EditAsync(labels);
    }
}
