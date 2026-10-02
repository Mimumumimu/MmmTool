using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Core.Services;

namespace MmmTool.ViewModels;

/// <summary>通知ウィンドウの ViewModel</summary>
/// <remarks>本文の Inlines の組み立ては View 側で行う（リンクのクリックを含むため）。</remarks>
public sealed partial class NotificationDialogViewModel(LinkOpener opener) : ObservableObject
{
    /// <summary>タイトル</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>本文の項目</summary>
    [ObservableProperty]
    public partial IReadOnlyList<NotificationItem> Items { get; set; } = [];

    /// <summary>リンク先を開く</summary>
    /// <remarks>開けなかったときは何も表示しない（通知に失敗の表示は仕様にないため）。</remarks>
    [RelayCommand]
    private async Task OpenLinkAsync(string path)
    {
        try
        {
            await opener.OpenAsync(path);
        }
        catch (LinkOpenException)
        {
        }
    }
}
