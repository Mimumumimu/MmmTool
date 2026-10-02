using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.Storage.Pickers;
using MmmTool.Views;

namespace MmmTool.Services;

public sealed class FolderPickerService(IServiceProvider services) : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        // Windows App SDK のピッカーは、アンパッケージでもウィンドウ ID を渡すだけで使える
        var picker = new FolderPicker(services.GetRequiredService<MainWindow>().AppWindow.Id);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }
}
