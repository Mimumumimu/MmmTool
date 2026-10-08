namespace MmmTool.Features.Database;

/// <summary>保存先の編集画面を開く</summary>
public interface IDatabaseDialogService
{
    /// <summary>保存先と接続を編集する画面を、モーダルで開き、閉じるまで待つ</summary>
    /// <returns>画面が閉じたことを表すタスク</returns>
    Task ShowEditAsync();
}
