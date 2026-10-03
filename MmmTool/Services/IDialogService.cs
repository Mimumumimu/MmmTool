using MmmTool.Core.Entities;

namespace MmmTool.Services;

/// <summary>
/// ダイアログを開く（ViewModel から UI 型に触れずに使うための口）。
/// </summary>
public interface IDialogService
{
    /// <summary>作業ディレクトリ変更ダイアログを開く</summary>
    /// <remarks>選ばれたフォルダ、キャンセルなら null。</remarks>
    Task<string?> ShowWorkingDirectoryDialogAsync();

    /// <summary>確認ダイアログを開く</summary>
    /// <remarks>実行するボタンが押されたら true。</remarks>
    /// <param name="primaryText">実行するボタンの文言（「削除」等）。</param>
    Task<bool> ConfirmAsync(string title, string message, string primaryText);

    /// <summary>リマインダー入力画面を、いちばん手前の画面の上にモーダルで開く</summary>
    /// <param name="reminder">編集するリマインダー。新規なら null（連番 0 の内容を渡すと、それを初期値にした新規）</param>
    /// <returns>保存した内容。キャンセルなら null</returns>
    Task<Reminder?> ShowReminderInputAsync(Reminder? reminder);

    /// <summary>リマインダー一覧画面を、いちばん手前の画面の上にモーダルで開き、閉じるまで待つ</summary>
    Task ShowReminderListAsync();
}
