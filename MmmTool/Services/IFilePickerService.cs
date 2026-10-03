namespace MmmTool.Services;

/// <summary>ファイル選択</summary>
/// <remarks>ViewModel から UI 型に触れずに使うための口。</remarks>
public interface IFilePickerService
{
    /// <summary>ファイル選択を開く</summary>
    /// <returns>選ばれたファイルのパス。キャンセルなら null</returns>
    Task<string?> PickFileAsync();
}
