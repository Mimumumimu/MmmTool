using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users.Edit;

/// <summary>
/// アカウントの項目 (表示名・ログイン名・パスワード)を 1 つ編集する画面の ViewModel。
/// </summary>
/// <remarks>
/// 項目は <see cref="Initialize"/> で決める。ログイン名とパスワードは、本人の確認のために、今のパスワードを入力させる。
/// 保存できたら <see cref="CloseRequested"/> で、画面を閉じてよいことを知らせる。
/// </remarks>
public sealed partial class AccountEditViewModel : ObservableObject
{
    /// <summary>今のパスワードが合わないときのエラー</summary>
    private const string WrongPasswordMessage = "現在のパスワードが違います。";

    /// <summary>アカウントの変更</summary>
    private readonly UserAccountService _account;

    /// <summary>今のユーザー</summary>
    private readonly CurrentUser _currentUser;

    /// <summary>編集する項目</summary>
    private AccountEditKind _kind;

    /// <summary>ViewModel を作る</summary>
    /// <param name="account">アカウントの変更</param>
    /// <param name="currentUser">今のユーザー</param>
    public AccountEditViewModel(UserAccountService account, CurrentUser currentUser)
    {
        _account = account;
        _currentUser = currentUser;
    }

    /// <summary>画面を閉じてよい</summary>
    public event EventHandler? CloseRequested;

    /// <summary>画面の見出し</summary>
    public string Heading => _kind switch
    {
        AccountEditKind.LoginName => "ログイン名の変更",
        AccountEditKind.Password => "パスワードの変更",
        _ => "表示名の変更",
    };

    /// <summary>名前の入力欄の見出し</summary>
    public string ValueHeader => _kind == AccountEditKind.LoginName ? "ログイン名" : "表示名";

    /// <summary>名前の最大文字数</summary>
    public int ValueMaxLength => _kind == AccountEditKind.LoginName ? AppUser.LoginNameMaxLength : AppUser.DisplayNameMaxLength;

    /// <summary>名前の入力欄を出すか (表示名・ログイン名)</summary>
    public bool ShowValue => _kind != AccountEditKind.Password;

    /// <summary>今のパスワードの入力欄を出すか (ログイン名・パスワード。本人の確認)</summary>
    public bool NeedsCurrentPassword => _kind != AccountEditKind.DisplayName;

    /// <summary>新しいパスワードの入力欄を出すか</summary>
    public bool IsPassword => _kind == AccountEditKind.Password;

    /// <summary>表示名・ログイン名の入力 (初期値は今の値)</summary>
    [ObservableProperty]
    public partial string Value { get; set; } = "";

    /// <summary>今のパスワード</summary>
    [ObservableProperty]
    public partial string CurrentPassword { get; set; } = "";

    /// <summary>新しいパスワード</summary>
    [ObservableProperty]
    public partial string NewPassword { get; set; } = "";

    /// <summary>新しいパスワードの確認入力</summary>
    [ObservableProperty]
    public partial string NewPasswordConfirm { get; set; } = "";

    /// <summary>入力のエラー。無ければ null</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>保存先 (DB)の失敗のエラー (接続できない・設定が足りない など)</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>編集する項目を決め、入力欄の初期値を入れる</summary>
    /// <param name="kind">編集する項目</param>
    /// <remarks>画面を開く前に 1 度だけ呼ぶ。</remarks>
    public void Initialize(AccountEditKind kind)
    {
        _kind = kind;
        Value = kind switch
        {
            AccountEditKind.LoginName => _currentUser.User?.LoginName ?? "",
            AccountEditKind.DisplayName => _currentUser.User?.DisplayName ?? "",
            _ => "",
        };
        OnPropertyChanged(string.Empty);
    }

    /// <summary>入力を検証して保存し、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>入力が正しくないときや、保存できなかったときは、エラーを出して閉じない。</remarks>
    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = null;
        SaveError.Clear();
        try
        {
            if (await TrySaveAsync())
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (LoginNameTakenException)
        {
            Error = "ログイン名はすでに使われています。";
        }
        catch (DataFileException ex)
        {
            SaveError.Show(ex.Message);
        }
        catch (SecretStoreException ex)
        {
            // 変更は DB に済んでいるが、この PC に覚えられなかった。次の起動で、ログインし直しになる
            SaveError.Show(ex.Message);
        }
    }

    /// <summary>保存せずに閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>項目に合わせて、検証と保存を行う</summary>
    /// <returns>保存した (変更が無くて、保存が要らなかった場合も含む)なら true。エラーを出したなら false</returns>
    private Task<bool> TrySaveAsync() => _kind switch
    {
        AccountEditKind.LoginName => SaveLoginNameAsync(),
        AccountEditKind.Password => SavePasswordAsync(),
        _ => SaveDisplayNameAsync(),
    };

    /// <summary>表示名を保存する</summary>
    /// <returns>保存した、または変更が無ければ true。入力が正しくなければ false</returns>
    private async Task<bool> SaveDisplayNameAsync()
    {
        if (Value.Trim().Length == 0)
        {
            Error = "表示名を入力してください。";
            return false;
        }
        if (Value.Trim() != _currentUser.User?.DisplayName)
        {
            await _account.ChangeDisplayNameAsync(Value);
        }
        return true;
    }

    /// <summary>ログイン名を保存する</summary>
    /// <returns>保存した、または変更が無ければ true。入力が正しくない・今のパスワードが合わなければ false</returns>
    private async Task<bool> SaveLoginNameAsync()
    {
        if (Value.Trim().Length == 0)
        {
            Error = "ログイン名を入力してください。";
            return false;
        }
        if (Value.Trim() == _currentUser.User?.LoginName)
        {
            return true;
        }
        if (!AppUser.IsValidLoginName(Value.Trim()))
        {
            Error = AppUser.InvalidLoginNameMessage;
            return false;
        }
        if (CurrentPassword.Length == 0)
        {
            Error = "現在のパスワードを入力してください。";
            return false;
        }
        if (!await _account.ChangeLoginNameAsync(Value, CurrentPassword))
        {
            Error = WrongPasswordMessage;
            return false;
        }
        return true;
    }

    /// <summary>パスワードを保存する</summary>
    /// <returns>保存したなら true。入力が正しくない・今のパスワードが合わなければ false</returns>
    private async Task<bool> SavePasswordAsync()
    {
        if (CurrentPassword.Length == 0)
        {
            Error = "現在のパスワードを入力してください。";
            return false;
        }
        if (NewPassword.Length == 0)
        {
            Error = "新しいパスワードを入力してください。";
            return false;
        }
        if (NewPassword != NewPasswordConfirm)
        {
            Error = "パスワードが一致しません。";
            return false;
        }
        if (!await _account.ChangePasswordAsync(CurrentPassword, NewPassword))
        {
            Error = WrongPasswordMessage;
            return false;
        }
        return true;
    }

    /// <summary>入力中に出すエラーを求める (ログイン名に使えない文字があるときだけ)</summary>
    /// <returns>エラーの文言。無ければ null</returns>
    /// <remarks>今のログイン名と同じなら出さない (ASCII 以外で登録済みのログイン名を、変えずに開いたとき)。</remarks>
    private string? GetLiveError()
    {
        var name = Value.Trim();
        return _kind == AccountEditKind.LoginName && name.Length > 0 && name != _currentUser.User?.LoginName && !AppUser.IsValidLoginName(name)
            ? AppUser.InvalidLoginNameMessage
            : null;
    }

    /// <summary>入力を変えたら、エラーを消す (ログイン名に使えない文字があるときは出す)</summary>
    /// <param name="value">変えたあとの値</param>
    partial void OnValueChanged(string value) => Error = GetLiveError();

    /// <summary>入力を変えたら、エラーを消す (ログイン名に使えない文字があるときは残す)</summary>
    /// <param name="value">変えたあとの値</param>
    partial void OnCurrentPasswordChanged(string value) => Error = GetLiveError();

    /// <summary>入力を変えたら、エラーを消す (ログイン名に使えない文字があるときは残す)</summary>
    /// <param name="value">変えたあとの値</param>
    partial void OnNewPasswordChanged(string value) => Error = GetLiveError();
}

/// <summary>アカウントの編集画面で編集する項目</summary>
public enum AccountEditKind
{
    /// <summary>表示名</summary>
    DisplayName,

    /// <summary>ログイン名</summary>
    LoginName,

    /// <summary>パスワード</summary>
    Password,
}
