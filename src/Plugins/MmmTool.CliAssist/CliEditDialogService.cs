using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Components.Dialogs;

namespace MmmTool.CliAssist;

/// <summary>CLI補助の、一覧を編集するダイアログを開く土台</summary>
/// <typeparam name="TDialog">開くダイアログ</typeparam>
/// <param name="services">ダイアログを作る DI のサービスプロバイダー</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>ダイアログは開くたびに DI から作る。UI スレッドから呼ぶ。</remarks>
public abstract class CliEditDialogService<TDialog>(IServiceProvider services, IDialogHost dialogs)
    where TDialog : ContentDialog, ICliEditDialog
{
    /// <summary>編集ダイアログを開く</summary>
    /// <returns>ダイアログが閉じるまでの完了を表すタスク。保存した内容は、保存先のサービスへ反映済み</returns>
    public Task ShowAsync()
    {
        var dialog = services.GetRequiredService<TDialog>();
        dialogs.Attach(dialog);
        return dialog.EditAsync();
    }
}
