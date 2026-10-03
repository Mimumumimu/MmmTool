namespace MmmTool.Features.CliAssist;

/// <summary>作業ディレクトリ変更ダイアログを開く（ViewModel から UI 型に触れずに使うための口）</summary>
public interface IWorkingDirectoryDialogService
{
    /// <summary>作業ディレクトリ変更ダイアログを開く</summary>
    /// <returns>選ばれたフォルダ。キャンセルなら null</returns>
    Task<string?> ShowAsync();
}
