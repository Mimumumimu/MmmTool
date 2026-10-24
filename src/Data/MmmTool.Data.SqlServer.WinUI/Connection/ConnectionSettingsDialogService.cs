using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>DB の接続の設定画面を開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>画面は閉じると再表示できないので、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する。UI スレッドから呼ぶ。</remarks>
public sealed class ConnectionSettingsDialogService(IServiceScopeFactory scopeFactory, IDialogHost dialogs) : IConnectionSettingsDialogService
{
    /// <inheritdoc />
    public event EventHandler? ConnectionSettingsClosed;

    /// <inheritdoc />
    public async Task ShowConnectionSettingsAsync()
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var window = scope.ServiceProvider.GetRequiredService<ConnectionSettingsWindow>();
            await dialogs.ShowModalAsync(window, async owner =>
            {
                await window.ShowModalAsync(owner);
                return true;
            });
        }

        ConnectionSettingsClosed?.Invoke(this, EventArgs.Empty);
    }
}
