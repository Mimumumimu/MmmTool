using MmmTool.Features.Users.Edit;

namespace MmmTool.Features.Users;

/// <summary>アカウントの編集画面を開く</summary>
public interface IAccountDialogService
{
    /// <summary>アカウントの項目を編集する画面を、モーダルで開き、閉じるまで待つ</summary>
    /// <param name="kind">編集する項目</param>
    /// <returns>画面が閉じたことを表すタスク</returns>
    Task ShowEditAsync(AccountEditKind kind);
}
