using Microsoft.UI.Dispatching;
using MmmSdk.WinUI.Components.Notifications;
using MmmTool.Core.Reminders;
using MmmTool.Features.Reminders.Main;
using MmmTool.Shell;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの起動時の準備（時刻監視の開始）</summary>
/// <param name="monitor">リマインダーの時刻監視</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="reminderWindows">リマインダーのメイン画面を開く</param>
/// <remarks>
/// 監視はタイマーのスレッドから通知を求めてくるので、UI スレッドに切り替えて通知ダイアログを出す。
/// 通知の本文をクリックして閉じたら、リマインダーのメイン画面を開く（発動済みの未対応はスヌーズに進む）。
/// </remarks>
public sealed class ReminderStartup(
    ReminderMonitor monitor,
    INotificationDialogService notifications,
    ReminderWindowService reminderWindows) : IStartupTask
{
    /// <inheritdoc />
    /// <remarks>UI スレッドから呼ぶ（通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使う）。</remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        monitor.Start(
            (title, items) => dispatcher.TryEnqueue(
                () => notifications.Show(title, items, onClicked: () => dispatcher.TryEnqueue(
                    // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                    DispatcherQueuePriority.Low, async () => await reminderWindows.ShowFromNotificationAsync()))));
        return Task.CompletedTask;
    }
}
