using Microsoft.Windows.Storage.Pickers;

namespace MmmTool.Services;

/// <summary>フォルダ選択を開く</summary>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
public sealed class FolderPickerService(DialogService dialogs) : IFolderPickerService
{
    /// <inheritdoc />
    public async Task<string?> PickFolderAsync()
    {
        // Windows App SDK のピッカーは、アンパッケージでもウィンドウ ID を渡すだけで使える
        var picker = new FolderPicker(dialogs.Owner.AppWindow.Id);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }
}
