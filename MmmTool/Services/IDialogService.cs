namespace MmmTool.Services;

/// <summary>
/// 機能をまたいで使うダイアログを開く（ViewModel から UI 型に触れずに使うための口）。
/// </summary>
/// <remarks>機能固有の画面を開く口は、各機能に置く（<c>IReminderDialogService</c> など）。</remarks>
public interface IDialogService
{
    /// <summary>確認ダイアログを開く</summary>
    /// <param name="title">ダイアログのタイトル</param>
    /// <param name="message">確認する内容のメッセージ</param>
    /// <param name="primaryText">実行するボタンの文言（「削除」等）。</param>
    /// <returns>実行するボタンが押されたら true</returns>
    Task<bool> ConfirmAsync(string title, string message, string primaryText);
}
