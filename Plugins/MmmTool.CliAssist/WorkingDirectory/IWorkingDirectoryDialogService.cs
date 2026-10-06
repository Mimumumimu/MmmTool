namespace MmmTool.CliAssist.WorkingDirectory;

/// <summary>作業ディレクトリ変更ダイアログを開く (ViewModel から UI 型に触れずに使うための口)</summary>
public interface IWorkingDirectoryDialogService
{
    /// <summary>フォルダを選ぶダイアログを開く</summary>
    /// <param name="title">ダイアログのタイトル</param>
    /// <param name="primaryButtonText">決定ボタンの文言</param>
    /// <param name="openDirectories">ほかのタブが開いているフォルダ (選べないようにする)</param>
    /// <returns>選ばれたフォルダ。キャンセルなら null</returns>
    Task<string?> ShowAsync(string title, string primaryButtonText, IReadOnlyList<string> openDirectories);
}
