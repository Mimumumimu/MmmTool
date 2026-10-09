using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>ダイアログは開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public sealed class QuickMessageDialogService(IServiceProvider services, IDialogHost dialogs) : IQuickMessageDialogService
{
    /// <inheritdoc />
    public Task ShowAsync()
    {
        var dialog = services.GetRequiredService<QuickMessageDialog>();
        dialogs.Attach(dialog);
        return dialog.EditAsync();
    }
}
