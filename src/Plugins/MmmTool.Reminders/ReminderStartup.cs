using Microsoft.UI.Dispatching;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Speech;
using MmmTool.Reminders.Core;
using MmmTool.Reminders.Main;

namespace MmmTool.Reminders;

/// <summary>リマインダーの起動時の準備</summary>
/// <param name="monitor">リマインダーの時刻監視</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="speech">読み上げ</param>
/// <param name="reminderWindows">リマインダーのメイン画面を開く</param>
/// <param name="features">機能のオン・オフと、管理者による無効の状態。</param>
/// <remarks>
/// 監視はタイマーのスレッドから通知を求めてくるので、UI スレッドに切り替えて通知ダイアログを出し、読み上げ対象があれば一緒に読み上げる。
/// 通知の本文をクリックして閉じたら、リマインダーのメイン画面を開く。
/// 発動済みの未対応はスヌーズに進む。
/// </remarks>
public sealed class ReminderStartup(
    ReminderMonitor monitor,
    INotificationDialogService notifications,
    ISpeechService speech,
    ReminderWindowService reminderWindows,
    IFeatureStatus features) : IStartupTask
{
    /// <summary>通知ダイアログの節の送り元のキー</summary>
    private const string NotificationSourceKey = "Reminders";

    /// <summary>通知ダイアログの節の並びの値</summary>
    /// <remarks>サイドバーの並びと同じで、ノートの下に出す。</remarks>
    private const int NotificationOrder = 4;

    /// <inheritdoc />
    /// <remarks>
    /// UI スレッドから呼ぶ。
    /// 通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使う。
    /// </remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        monitor.Start(
            (title, items, speechText) => dispatcher.TryEnqueue(
                () =>
                {
                    notifications.Show(new NotificationSection(NotificationSourceKey, NotificationOrder, title, items, OnClicked: () =>
                    {
                        reminderWindows.MarkClicked(); // TEMP(計測)
                        dispatcher.TryEnqueue(
                            // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                            DispatcherQueuePriority.Low, async () => await reminderWindows.ShowFromNotificationAsync());
                    }));
                    if (speechText is not null)
                    {
                        speech.SpeakAsync(speechText).Forget();
                    }
                }),
            // 管理者に無効にされた人では、判定しない。保存先は最初に読むまで動かないので、起動時から無効の人は、DB も読まない
            () => features.IsEnabled(RemindersFeature.Key));
        return Task.CompletedTask;
    }
}
