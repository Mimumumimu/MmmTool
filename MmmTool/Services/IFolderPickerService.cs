namespace MmmTool.Services;

public interface IFolderPickerService
{
    /// <summary>フォルダ選択を開く。選ばれたフォルダのパス、キャンセルなら null。</summary>
    Task<string?> PickFolderAsync();
}
