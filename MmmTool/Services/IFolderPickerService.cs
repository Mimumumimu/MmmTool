namespace MmmTool.Services;

/// <summary>フォルダ選択</summary>
/// <remarks>ViewModel から UI 型に触れずに使うための口。</remarks>
public interface IFolderPickerService
{
    /// <summary>フォルダ選択を開く</summary>
    /// <returns>選ばれたフォルダのパス。キャンセルなら null</returns>
    Task<string?> PickFolderAsync();
}
