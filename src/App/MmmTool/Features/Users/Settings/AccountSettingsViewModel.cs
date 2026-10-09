using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Data.Connection;
using MmmTool.Features.Users.Edit;
using MmmTool.Shell;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users.Settings;

/// <summary>
/// アカウント (表示名・ログイン名・パスワードの変更とログアウト)の設定ページの部品の ViewModel。
/// </summary>
/// <remarks>
/// DB に保存していて、ログイン済みのときだけ出す (ローカルモードには、ユーザーがいない)。保存先は起動時に決まるので、出すかどうかは、部品を作るときに決める。
/// 変更は、別の画面 (<see cref="AccountEditWindow"/>)で行う。ここは、閉じたあとに、見せている内容を読み直す。
/// </remarks>
public sealed partial class AccountSettingsViewModel : ObservableObject
{
    /// <summary>今のユーザー</summary>
    private readonly CurrentUser _currentUser;

    /// <summary>アカウントの変更とログアウト</summary>
    private readonly UserAccountService _account;

    /// <summary>編集画面を開く</summary>
    private readonly IAccountDialogService _editDialogs;

    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _dialogs;

    /// <summary>アプリの終了を頼む口</summary>
    private readonly AppExitService _exit;

    /// <summary>ViewModel を作り、今のユーザーを読み込む</summary>
    /// <param name="settings">DB への接続の設定</param>
    /// <param name="currentUser">今のユーザー</param>
    /// <param name="account">アカウントの変更とログアウト</param>
    /// <param name="editDialogs">編集画面を開く</param>
    /// <param name="dialogs">確認ダイアログを開く</param>
    /// <param name="exit">アプリの終了を頼む口</param>
    public AccountSettingsViewModel(
        DatabaseSettingsService settings, CurrentUser currentUser, UserAccountService account,
        IAccountDialogService editDialogs, IDialogService dialogs, AppExitService exit)
    {
        _currentUser = currentUser;
        _account = account;
        _editDialogs = editDialogs;
        _dialogs = dialogs;
        _exit = exit;
        IsAvailable = settings.Load().Mode == DatabaseMode.SqlServer && currentUser.IsIdentified;
        Refresh();
    }

    /// <summary>この部品を出すか (DB に保存していて、ログイン済み)</summary>
    public bool IsAvailable { get; }

    /// <summary>表示名</summary>
    [ObservableProperty]
    public partial string DisplayName { get; private set; } = "";

    /// <summary>ログイン名</summary>
    [ObservableProperty]
    public partial string LoginName { get; private set; } = "";

    /// <summary>画面に出すエラー (ログイン情報を消せなかったとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>表示名の編集画面を開く</summary>
    /// <returns>画面が閉じるまでの完了を表すタスク</returns>
    [RelayCommand]
    private Task EditDisplayNameAsync() => EditAsync(AccountEditKind.DisplayName);

    /// <summary>ログイン名の編集画面を開く</summary>
    /// <returns>画面が閉じるまでの完了を表すタスク</returns>
    [RelayCommand]
    private Task EditLoginNameAsync() => EditAsync(AccountEditKind.LoginName);

    /// <summary>パスワードの編集画面を開く</summary>
    /// <returns>画面が閉じるまでの完了を表すタスク</returns>
    [RelayCommand]
    private Task EditPasswordAsync() => EditAsync(AccountEditKind.Password);

    /// <summary>確認のあと、ログイン情報を消して、アプリを終了する</summary>
    /// <returns>操作の完了を表すタスク</returns>
    [RelayCommand]
    private async Task SignOutAsync()
    {
        Error.Clear();
        if (!await _dialogs.ConfirmAsync("ログアウトしますか？", "保存してあるログイン情報を消して、アプリを終了します。", "ログアウト", "キャンセル"))
        {
            return;
        }

        try
        {
            _account.SignOut();
        }
        catch (SecretStoreException ex)
        {
            Error.Show(ex.Message);
            return;
        }
        _exit.RequestExit();
    }

    /// <summary>編集画面を開き、閉じたら見せている内容を読み直す</summary>
    /// <param name="kind">編集する項目</param>
    /// <returns>画面が閉じるまでの完了を表すタスク</returns>
    private async Task EditAsync(AccountEditKind kind)
    {
        await _editDialogs.ShowEditAsync(kind);
        Refresh();
    }

    /// <summary>今のユーザーから、見せている内容を読み直す</summary>
    private void Refresh()
    {
        DisplayName = _currentUser.User?.DisplayName ?? "";
        LoginName = _currentUser.User?.LoginName ?? "";
    }
}
