namespace MmmTool.Services;

/// <summary>
/// ダイアログを開く（ViewModel から UI 型に触れずに使うための口）。
/// </summary>
public interface IDialogService
{
    /// <summary>作業ディレクトリ変更ダイアログを開く。選ばれたフォルダ、キャンセルなら null。</summary>
    Task<string?> ShowWorkingDirectoryDialogAsync();
}
