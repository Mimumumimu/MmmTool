using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Dialogs;
using MmmTool.Core.Reminders;
using MmmTool.Features.Reminders.Input;
using MmmTool.Features.Reminders.List;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの入力・一覧画面を開く</summary>
/// <param name="services">画面を作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>画面は閉じると再表示できないので、開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public sealed class ReminderDialogService(IServiceProvider services, IDialogHost dialogs) : IReminderDialogService
{
    /// <inheritdoc />
    public Task<Reminder?> ShowInputAsync(Reminder? reminder)
    {
        var window = services.GetRequiredService<ReminderInputWindow>();
        return dialogs.ShowModalAsync(window, owner => window.ShowModalAsync(owner, reminder));
    }

    /// <inheritdoc />
    public Task ShowListAsync()
    {
        var window = services.GetRequiredService<ReminderListWindow>();
        return dialogs.ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }
}
