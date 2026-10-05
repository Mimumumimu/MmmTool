using Microsoft.UI.Dispatching;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Speech;
using MmmTool.Core.Reminders;
using MmmTool.Features.Reminders.Main;
using MmmTool.Shell;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの起動時の準備 (時刻監視の開始)</summary>
/// <param name="monitor">リマインダーの時刻監視</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="speech">読み上げ</param>
/// <param name="reminderWindows">リマインダーのメイン画面を開く</param>
/// <remarks>
/// 監視はタイマーのスレッドから通知を求めてくるので、UI スレッドに切り替えて通知ダイアログを出し、読み上げ対象があれば一緒に読み上げる。
/// 通知の本文をクリックして閉じたら、リマインダーのメイン画面を開く (発動済みの未対応はスヌーズに進む)。
/// </remarks>
public sealed class ReminderStartup(
    ReminderMonitor monitor,
    INotificationDialogService notifications,
    ISpeechService speech,
    ReminderWindowService reminderWindows) : IStartupTask
{
    /// <inheritdoc />
    /// <remarks>UI スレッドから呼ぶ (通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使う)。</remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        monitor.Start(
            (title, items, speechText) => dispatcher.TryEnqueue(
                () =>
                {
                    notifications.Show(title, items, onClicked: () => dispatcher.TryEnqueue(
                        // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                        DispatcherQueuePriority.Low, async () => await reminderWindows.ShowFromNotificationAsync()));
                    if (speechText is not null)
                    {
                        speech.SpeakAsync(speechText).Forget();
                    }
                }));
        return Task.CompletedTask;
    }
}
