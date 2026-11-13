using MmmTool.Reminders.Core;

namespace MmmTool.Reminders;

/// <summary>リマインダーの入力・一覧画面を開く</summary>
public interface IReminderDialogService
{
    /// <summary>リマインダー入力画面をいちばん手前の画面の上にモーダルで開く</summary>
    /// <param name="reminder">
    /// 編集するリマインダー。
    /// 新規なら null (連番 0 の内容を渡すと、それを初期値にした新規)。
    /// </param>
    /// <param name="copySourceNo">
    /// コピーして新規追加のときの、コピー元の連番で、送信先の引き継ぎに使う。
    /// それ以外は 0。
    /// </param>
    /// <returns>
    /// 保存した内容
    /// キャンセルなら null
    /// </returns>
    Task<Reminder?> ShowInputAsync(Reminder? reminder, int copySourceNo = 0);

    /// <summary>まとめて追加の画面をいちばん手前の画面の上にモーダルで開く</summary>
    /// <returns>
    /// 登録した件数
    /// キャンセルなら 0
    /// </returns>
    Task<int> ShowBulkInputAsync();

    /// <summary>リマインダー一覧画面をいちばん手前の画面の上にモーダルで開き、閉じるまで待つ</summary>
    /// <returns>一覧画面が閉じるまでの待機を表すタスク</returns>
    Task ShowListAsync();
}
