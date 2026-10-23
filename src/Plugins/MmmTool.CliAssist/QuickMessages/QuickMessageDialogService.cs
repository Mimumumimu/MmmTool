using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ウィンドウを開く</summary>
/// <param name="services">ウィンドウを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ウィンドウの親を決めるサービス</param>
/// <remarks>ウィンドウは開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public sealed class QuickMessageDialogService(IServiceProvider services, IDialogHost dialogs) : IQuickMessageDialogService
{
    /// <inheritdoc />
    public async Task ShowAsync()
    {
        var window = services.GetRequiredService<QuickMessageWindow>();
        await dialogs.ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }
}
