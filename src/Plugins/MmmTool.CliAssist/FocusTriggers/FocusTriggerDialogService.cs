using MmmSdk.WinUI.Components.Dialogs;

namespace MmmTool.CliAssist.FocusTriggers;

/// <summary>ターミナルへ移る文字列の編集ダイアログを開く</summary>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
public sealed class FocusTriggerDialogService(IServiceProvider services, IDialogHost dialogs)
    : CliEditDialogService<FocusTriggerDialog>(services, dialogs), IFocusTriggerDialogService
{
}
