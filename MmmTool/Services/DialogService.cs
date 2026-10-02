using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public async Task<bool> ConfirmAsync(string title, string message, string primaryText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = services.GetRequiredService<MainWindow>().Content.XamlRoot,
            // コードで作るときは既定のスタイルが当たらないため、明示する（付けないと旧来の見た目になる）
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = "キャンセル",
            // 取り消しにくい操作なので、Enter で誤って実行しないようキャンセルを既定にする
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
