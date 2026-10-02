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
}
