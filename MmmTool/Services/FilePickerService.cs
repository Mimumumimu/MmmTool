using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.Storage.Pickers;
using MmmTool.Views;

namespace MmmTool.Services;

public sealed class FilePickerService(IServiceProvider services) : IFilePickerService
{
    public async Task<string?> PickFileAsync()
    {
        var picker = new FileOpenPicker(services.GetRequiredService<MainWindow>().AppWindow.Id);
        picker.FileTypeFilter.Add("*");
        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }
}
