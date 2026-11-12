namespace MmmTool.CliAssist.FocusTriggers;

/// <summary>通知する文字列の編集ダイアログを開く</summary>
public interface IFocusTriggerDialogService
{
    /// <summary>編集ダイアログを開く</summary>
    /// <returns>
    /// ダイアログが閉じるまでの完了を表すタスク
    /// 保存した内容は通知する文字列のサービスへ反映済み
    /// </returns>
    Task ShowAsync();
}
