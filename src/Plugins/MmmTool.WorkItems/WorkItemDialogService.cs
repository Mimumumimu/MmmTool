using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.WorkItems.Move;
using MmmTool.WorkItems.ProjectManager;

namespace MmmTool.WorkItems;

/// <summary>作業管理のダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>
/// ダイアログは開くたびに DI から作る。UI スレッドから呼ぶ。案件の管理のウィンドウ (と ViewModel)は、開くたびに作るスコープから解決し、
/// 閉じたらスコープごと破棄する (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため)。
/// </remarks>
public sealed class WorkItemDialogService(IServiceProvider services, IServiceScopeFactory scopeFactory, IDialogHost dialogs) : IWorkItemDialogService
{
    /// <inheritdoc />
    public Task<MoveDestination?> PickDestinationAsync(IReadOnlyList<MoveDestination> destinations)
    {
        var dialog = services.GetRequiredService<MoveDialog>();
        dialogs.Attach(dialog);
        return dialog.PickAsync(destinations);
    }

    /// <inheritdoc />
    public async Task ShowProjectManagerAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<ProjectManagerWindow>();
        await dialogs.ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }
}
