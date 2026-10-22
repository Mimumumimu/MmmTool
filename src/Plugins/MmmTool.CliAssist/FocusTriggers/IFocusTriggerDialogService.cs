namespace MmmTool.CliAssist.FocusTriggers;

/// <summary>ターミナルへ移る文字列の編集ダイアログを開く (ViewModel から UI 型に触れずに使うための口)</summary>
public interface IFocusTriggerDialogService
{
    /// <summary>編集ダイアログを開く</summary>
    /// <returns>ダイアログが閉じるまでの完了を表すタスク。保存した内容は、ターミナルへ移る文字列のサービスへ反映済み</returns>
    Task ShowAsync();
}
