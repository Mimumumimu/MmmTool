namespace MmmTool.CliAssist;

/// <summary>CLI補助の、一覧を編集するダイアログ</summary>
public interface ICliEditDialog
{
    /// <summary>ダイアログを開く</summary>
    /// <returns>ダイアログが閉じるまでの完了を表すタスク</returns>
    Task EditAsync();
}
