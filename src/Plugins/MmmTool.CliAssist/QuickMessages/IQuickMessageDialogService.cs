namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログを開く</summary>
public interface IQuickMessageDialogService
{
    /// <summary>編集ダイアログを開く</summary>
    /// <returns>ダイアログが閉じるまでの完了を表すタスク (保存した内容はよく使う文のサービスへ反映済み)</returns>
    Task ShowAsync();
}
