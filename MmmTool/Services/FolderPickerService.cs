using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.Storage.Pickers;
using MmmTool.Views;

namespace MmmTool.Services;

/// <summary>フォルダ選択を開く</summary>
/// <param name="services">メインウィンドウを取得する DI のサービスプロバイダー</param>
public sealed class FolderPickerService(IServiceProvider services) : IFolderPickerService
{
    /// <inheritdoc />
    public async Task<string?> PickFolderAsync()
    {
        // Windows App SDK のピッカーは、アンパッケージでもウィンドウ ID を渡すだけで使える
        var picker = new FolderPicker(services.GetRequiredService<MainWindow>().AppWindow.Id);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }
}
