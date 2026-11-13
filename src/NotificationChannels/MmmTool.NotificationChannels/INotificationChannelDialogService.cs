using MmmTool.NotificationChannels.Core;

namespace MmmTool.NotificationChannels;

/// <summary>送信先の一覧・登録の画面を開く</summary>
public interface INotificationChannelDialogService
{
    /// <summary>送信先の一覧画面を開いて閉じるまで待つ</summary>
    /// <returns>一覧画面が閉じるまでの待機を表すタスク</returns>
    /// <remarks>
    /// いちばん手前の画面の上にモーダルで開く
    /// DB モードだけ
    /// </remarks>
    Task ShowChannelListAsync();

    /// <summary>送信先の登録画面をいちばん手前の画面の上にモーダルで開く</summary>
    /// <param name="channel">
    /// 編集する送信先
    /// 新規なら null
    /// </param>
    /// <returns>
    /// 保存した内容
    /// キャンセルなら null
    /// </returns>
    /// <remarks>DB モードだけ</remarks>
    Task<NotificationChannel?> ShowChannelEditAsync(NotificationChannel? channel);
}
