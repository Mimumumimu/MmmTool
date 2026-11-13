using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.NotificationChannels.ChannelEdit;
using MmmTool.NotificationChannels.ChannelList;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.NotificationChannels;

/// <summary>送信先の一覧・登録の画面を開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>
/// 画面は閉じると再表示できないので、開くたびに DI から作る。
/// UI スレッドから呼ぶ。
/// 画面 (と ViewModel)は開くたびに作るスコープから解決し、閉じたらスコープごと破棄する。
/// ルートから解決した <see cref="IDisposable"/> の Transient はアプリの終了まで DI コンテナが保持し続けるため。
/// </remarks>
public sealed class NotificationChannelDialogService(IServiceScopeFactory scopeFactory, IDialogHost dialogs) : INotificationChannelDialogService
{
    /// <inheritdoc />
    public async Task ShowChannelListAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<NotificationChannelListWindow>();
        await dialogs.ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }

    /// <inheritdoc />
    public async Task<NotificationChannel?> ShowChannelEditAsync(NotificationChannel? channel)
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<NotificationChannelEditWindow>();
        return await dialogs.ShowModalAsync(window, owner => window.ShowModalAsync(owner, channel));
    }
}
