namespace MmmTool.Services;

public interface IFilePickerService
{
    /// <summary>ファイル選択を開く。選ばれたファイルのパス、キャンセルなら null。</summary>
    Task<string?> PickFileAsync();
}
