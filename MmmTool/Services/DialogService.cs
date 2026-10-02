using Microsoft.Extensions.DependencyInjection;
using MmmTool.Views;
using MmmTool.Views.Dialogs;

namespace MmmTool.Services;

public sealed class DialogService(IServiceProvider services) : IDialogService
{
    public Task<string?> ShowWorkingDirectoryDialogAsync()
    {
        var dialog = services.GetRequiredService<WorkingDirectoryDialog>();
        dialog.XamlRoot = services.GetRequiredService<MainWindow>().Content.XamlRoot;
        return dialog.PickAsync();
    }
}
