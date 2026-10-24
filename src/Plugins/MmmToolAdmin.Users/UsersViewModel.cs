using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.Data.SqlServer.WinUI.Connection;
using MmmTool.Users.Core;
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Auth.Core.Enrollment;

namespace MmmToolAdmin.Users;

/// <summary>
/// ユーザーのページの ViewModel。ユーザーの一覧を出し、選んだ人の登録用コードを発行する。
/// </summary>
/// <param name="users">ユーザーの保存先</param>
/// <param name="lookup">今日使える状態のユーザーを探す</param>
/// <param name="codes">登録用コードの発行</param>
/// <param name="dialogs">接続の設定の画面を開く</param>
/// <param name="confirm">発行の前の確認ダイアログ</param>
/// <param name="time">今日の日付を決める時計</param>
public sealed partial class UsersViewModel(
    IAppUserRepository users,
    WebUserLookup lookup,
    EnrollmentCodeService codes,
    IConnectionSettingsDialogService dialogs,
    IDialogService confirm,
    TimeProvider time) : DatabaseViewModelBase(dialogs)
{
    /// <summary>ユーザーの一覧で、番号の順</summary>
    public ObservableCollection<UserItem> Items { get; } = [];

    /// <summary>一覧で選んでいるユーザー。無ければ null</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IssueCodeCommand))]
    public partial UserItem? Selected { get; set; }

    /// <summary>発行した登録用コードを出しているか</summary>
    [ObservableProperty]
    public partial bool IsIssuedOpen { get; set; }

    /// <summary>発行した登録用コードの見出しで、持ち主の名前を含む</summary>
    [ObservableProperty]
    public partial string IssuedTitle { get; private set; } = "";

    /// <summary>発行した登録用コード</summary>
    [ObservableProperty]
    public partial string IssuedCode { get; private set; } = "";

    /// <summary>発行した登録用コードの、期限と使い方の説明</summary>
    [ObservableProperty]
    public partial string IssuedMessage { get; private set; } = "";

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        var version = NextVersion();
        IReadOnlyList<AppUser> loaded;
        try
        {
            loaded = await users.GetUsersAsync();
        }
        catch (DataFileException ex)
        {
            if (IsCurrent(version))
            {
                Error.Show(ex.Message);
            }
            return;
        }

        if (!IsCurrent(version))
        {
            return;
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var selectedId = Selected?.Id;
        Items.Clear();
        foreach (var user in loaded)
        {
            Items.Add(UserItem.From(user, today));
        }

        // 読み直したあとも、同じ人を選んだままにする
        Selected = Items.FirstOrDefault(item => item.Id == selectedId);
        Error.Clear();
    }

    /// <summary>選んだ人の登録用コードを発行する</summary>
    /// <returns>発行の完了を表すタスク</returns>
    /// <remarks>前に出したコードは使えなくなる。使えるのは、今日使える状態のユーザーだけ。</remarks>
    [RelayCommand(CanExecute = nameof(CanIssueCode))]
    private async Task IssueCodeAsync()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        // 前のコードが使えなくなり、コードを知った人は、その人として登録できるので、必ず確認する
        if (!await confirm.ConfirmAsync(
            "登録用コードの発行",
            $"{selected.DisplayName} さんの登録用コードを発行します。前に出したコードは使えなくなります。",
            "発行",
            "キャンセル"))
        {
            return;
        }

        try
        {
            var user = await lookup.FindActiveAsync(selected.Id, CancellationToken.None);
            if (user is null)
            {
                // 読み直すと、エラーが消える。先に読み直してから出す
                await RefreshAsync();
                Error.Show($"{selected.DisplayName} さんは、今は使えない状態です。");
                return;
            }

            var issued = await codes.IssueAsync(user.Id, CancellationToken.None);
            IssuedTitle = $"{user.DisplayName} さんの登録用コード";
            IssuedCode = issued.Code;
            IssuedMessage = $"{issued.ExpiresAt.ToLocalTime():yyyy-MM-dd HH:mm} まで、1 回だけ使えます。前に出したコードは使えなくなります。";
            IsIssuedOpen = true;
            Error.Clear();
        }
        catch (Exception ex) when (ex is DataFileException or InvalidOperationException)
        {
            // DB に接続できない・表が無いなど。画面に出せるメッセージを見せる
            Error.Show(ex.Message);
        }
    }

    /// <summary>登録用コードを発行できるか</summary>
    /// <returns>今日使える状態のユーザーを選んでいれば true</returns>
    private bool CanIssueCode() => Selected is { IsActive: true };
}
