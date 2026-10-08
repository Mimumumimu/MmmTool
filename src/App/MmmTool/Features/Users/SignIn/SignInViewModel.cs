using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users.SignIn;

/// <summary>
/// ログインの画面。ログイン名とパスワードでログインする。新規登録と、再設定のあとの新しいパスワードも、同じ画面で扱う。
/// </summary>
/// <remarks>
/// 状態は <see cref="SignInMode"/> の 3 つ。ログインできたら (登録・新しいパスワードを決めた場合も)、今のユーザーが設定され、
/// <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class SignInViewModel : ObservableObject
{
    /// <summary>ログインに失敗したときのエラー</summary>
    private const string SignInFailedMessage = "ログイン名かパスワードが違います。";

    /// <summary>ログイン</summary>
    private readonly UserSignInService _signIn;

    /// <summary>現在日時</summary>
    private readonly TimeProvider _time;

    /// <summary>ViewModel を作る</summary>
    /// <param name="signIn">ログイン</param>
    /// <param name="time">現在日時の提供元</param>
    public SignInViewModel(UserSignInService signIn, TimeProvider time)
    {
        _signIn = signIn;
        _time = time;
        DisplayName = Environment.UserName;
    }

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>ログインしたユーザーを渡す。</remarks>
    public event EventHandler<AppUser?>? CloseRequested;

    /// <summary>ログイン名の最大文字数</summary>
    public int LoginNameMaxLength => AppUser.LoginNameMaxLength;

    /// <summary>表示名の最大文字数</summary>
    public int DisplayNameMaxLength => AppUser.DisplayNameMaxLength;

    /// <summary>画面の状態</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignInMode), nameof(IsRegisterMode), nameof(IsNewPasswordMode), nameof(IsLoginNameEditable))]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(PrimaryText), nameof(SecondaryText), nameof(HasSecondary))]
    public partial SignInMode Mode { get; private set; } = SignInMode.SignIn;

    /// <summary>ログインの状態か</summary>
    public bool IsSignInMode => Mode == SignInMode.SignIn;

    /// <summary>新規登録の状態か</summary>
    public bool IsRegisterMode => Mode == SignInMode.Register;

    /// <summary>新しいパスワードを決める状態か</summary>
    public bool IsNewPasswordMode => Mode == SignInMode.NewPassword;

    /// <summary>ログイン名を入力できるか (新しいパスワードを決めるときは、決まっているので入力しない)</summary>
    public bool IsLoginNameEditable => Mode != SignInMode.NewPassword;

    /// <summary>見出し</summary>
    public string Heading => Mode switch
    {
        SignInMode.Register => "新規登録",
        SignInMode.NewPassword => "新しいパスワードを決める",
        _ => "ログイン",
    };

    /// <summary>主なボタンの文言</summary>
    public string PrimaryText => Mode switch
    {
        SignInMode.Register => "登録",
        SignInMode.NewPassword => "決める",
        _ => "ログイン",
    };

    /// <summary>副のボタンの文言 (無いときは空)</summary>
    public string SecondaryText => Mode switch
    {
        SignInMode.SignIn => "新規登録",
        SignInMode.Register => "ログインに戻る",
        _ => "",
    };

    /// <summary>副のボタンがあるか</summary>
    public bool HasSecondary => SecondaryText.Length > 0;

    /// <summary>ログイン名</summary>
    [ObservableProperty]
    public partial string LoginName { get; set; } = "";

    /// <summary>表示名 (新規登録のとき。初期値は Windows のユーザー名)</summary>
    [ObservableProperty]
    public partial string DisplayName { get; set; } = "";

    /// <summary>パスワード</summary>
    [ObservableProperty]
    public partial string Password { get; set; } = "";

    /// <summary>パスワードの確認入力 (新規登録・新しいパスワードのとき)</summary>
    [ObservableProperty]
    public partial string PasswordConfirm { get; set; } = "";

    /// <summary>エラー。無ければ null</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>保存先 (DB)の失敗のエラー (接続できない・設定が足りない など)</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>入力を検証して、主な操作 (ログイン・登録・新しいパスワードを決める)を行う</summary>
    /// <returns>操作の完了を表すタスク</returns>
    /// <remarks>入力が正しくないときはエラーを出して、閉じない。</remarks>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        Error = null;
        SaveError.Clear();
        try
        {
            switch (Mode)
            {
                case SignInMode.SignIn:
                    await SignInAsync();
                    break;
                case SignInMode.Register:
                    await RegisterAsync();
                    break;
                default:
                    await SetNewPasswordAsync();
                    break;
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
    }

    /// <summary>副の操作 (ログインと新規登録を切り替える)を行う</summary>
    [RelayCommand]
    private void Secondary()
    {
        Error = null;
        Password = "";
        PasswordConfirm = "";
        Mode = Mode == SignInMode.SignIn ? SignInMode.Register : SignInMode.SignIn;
    }

    /// <summary>ログインする</summary>
    /// <returns>ログインの完了を表すタスク</returns>
    private async Task SignInAsync()
    {
        if (LoginName.Trim().Length == 0 || Password.Length == 0)
        {
            Error = SignInFailedMessage;
            return;
        }

        var result = await _signIn.SignInAsync(LoginName, Password, Today());
        switch (result.Status)
        {
            case SignInStatus.Succeeded:
                CloseRequested?.Invoke(this, result.User);
                break;
            case SignInStatus.NeedsNewPassword:
                Password = "";
                PasswordConfirm = "";
                Mode = SignInMode.NewPassword;
                break;
            default:
                Error = SignInFailedMessage;
                break;
        }
    }

    /// <summary>新規登録する</summary>
    /// <returns>登録の完了を表すタスク</returns>
    private async Task RegisterAsync()
    {
        if (LoginName.Trim().Length == 0)
        {
            Error = "ログイン名を入力してください。";
            return;
        }
        if (LoginName.Any(char.IsControl))
        {
            Error = "ログイン名に使えない文字があります。";
            return;
        }
        if (DisplayName.Trim().Length == 0)
        {
            Error = "表示名を入力してください。";
            return;
        }
        if (!ValidatePassword())
        {
            return;
        }

        var user = await _signIn.RegisterAsync(LoginName, DisplayName, Password, Today());
        CloseRequested?.Invoke(this, user);
    }

    /// <summary>新しいパスワードを決める</summary>
    /// <returns>決める操作の完了を表すタスク</returns>
    private async Task SetNewPasswordAsync()
    {
        if (!ValidatePassword())
        {
            return;
        }

        var user = await _signIn.SetNewPasswordAsync(LoginName, Password, Today());
        if (user is null)
        {
            // 先にほかの人が決めた・ユーザーが使えなくなった。ログインからやり直す
            Password = "";
            PasswordConfirm = "";
            Mode = SignInMode.SignIn;
            Error = SignInFailedMessage;
            return;
        }
        CloseRequested?.Invoke(this, user);
    }

    /// <summary>パスワードと確認入力を検証する</summary>
    /// <returns>正しければ true。正しくなければ、エラーを出して false</returns>
    private bool ValidatePassword()
    {
        if (Password.Length == 0)
        {
            Error = "パスワードを入力してください。";
            return false;
        }
        if (Password != PasswordConfirm)
        {
            Error = "パスワードが一致しません。";
            return false;
        }
        return true;
    }

    /// <summary>今日の日付を取得する</summary>
    /// <returns>今日の日付</returns>
    private DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    /// <summary>入力を変えたら、エラーを消す</summary>
    /// <param name="value">変えたあとの値</param>
    partial void OnLoginNameChanged(string value) => Error = null;

    /// <summary>入力を変えたら、エラーを消す</summary>
    /// <param name="value">変えたあとの値</param>
    partial void OnPasswordChanged(string value) => Error = null;
}

/// <summary>ログインの画面の状態</summary>
public enum SignInMode
{
    /// <summary>ログイン名とパスワードでログインする</summary>
    SignIn,

    /// <summary>新しくユーザーを登録する</summary>
    Register,

    /// <summary>管理者が再設定したあと、新しいパスワードを決める</summary>
    NewPassword,
}
