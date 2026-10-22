using MmmSdk.WinUI.Components.Dialogs;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
public sealed class QuickMessageDialogService(IServiceProvider services, IDialogHost dialogs)
    : CliEditDialogService<QuickMessageDialog>(services, dialogs), IQuickMessageDialogService
{
}
