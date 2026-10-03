using MmmTool.Core.Reminders;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの入力・一覧画面を開く（ViewModel から UI 型に触れずに使うための口）</summary>
public interface IReminderDialogService
{
    /// <summary>リマインダー入力画面を、いちばん手前の画面の上にモーダルで開く</summary>
    /// <param name="reminder">編集するリマインダー。新規なら null（連番 0 の内容を渡すと、それを初期値にした新規）</param>
    /// <returns>保存した内容。キャンセルなら null</returns>
    Task<Reminder?> ShowInputAsync(Reminder? reminder);

    /// <summary>リマインダー一覧画面を、いちばん手前の画面の上にモーダルで開き、閉じるまで待つ</summary>
    /// <returns>一覧画面が閉じるまでの待機を表すタスク</returns>
    Task ShowListAsync();
}
