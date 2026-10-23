using MmmTool.CodeMaster.Core;

namespace MmmTool.CodeMaster.Edit;

/// <summary>コードマスタの名前の編集画面を開く</summary>
public interface ICodeMasterDialogService
{
    /// <summary>区分の名前を編集する画面を、モーダルで開き、閉じるまで待つ</summary>
    /// <param name="editor">編集する区分</param>
    /// <returns>画面が閉じたことを表すタスク</returns>
    Task ShowEditAsync(CodeCategoryEditor editor);
}
