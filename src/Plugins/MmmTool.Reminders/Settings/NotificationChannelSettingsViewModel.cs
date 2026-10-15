using CommunityToolkit.Mvvm.Input;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>設定ページの「送信先」の ViewModel (送信先の一覧画面を開く)</summary>
/// <param name="dialogs">送信先の画面を開く</param>
/// <param name="channels">送信先の保存先 (DB モードかどうかの判定に使う)</param>
public sealed partial class NotificationChannelSettingsViewModel(IReminderDialogService dialogs, INotificationChannelRepository channels)
{
    /// <summary>DB モード (サーバーモード)か。false のときは、部品を出さない</summary>
    public bool IsAvailable { get; } = channels.IsAvailable;

    /// <summary>送信先の一覧画面を開く</summary>
    /// <returns>一覧画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task OpenAsync() => dialogs.ShowChannelListAsync();
}
