using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.Features.Database.Edit;

namespace MmmTool.Features.Database;

/// <summary>保存先の編集画面を開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>
/// 画面は閉じると再表示できないので、開くたびに DI から作る。UI スレッドから呼ぶ。
/// 画面 (と ViewModel)は、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する
/// (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため)。
/// </remarks>
public sealed class DatabaseDialogService(IServiceScopeFactory scopeFactory, IDialogHost dialogs) : IDatabaseDialogService
{
    /// <inheritdoc />
    public async Task ShowEditAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<DatabaseEditWindow>();
        await dialogs.ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }
}
