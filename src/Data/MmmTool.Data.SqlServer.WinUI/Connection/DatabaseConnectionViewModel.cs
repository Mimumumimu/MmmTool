using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Db.SqlServer.Components.Connections;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>
/// DB への接続の入力欄 (サーバー・データベース名・ユーザー名・パスワード)の ViewModel。
/// </summary>
/// <remarks>
/// 設定ページの「保存先」と、初回の保存先の選択の画面が、同じ入力欄 (<see cref="DatabaseConnectionForm"/>)として使う。ログインは、ユーザー名とパスワードで固定する。
/// 「接続を確認」で、つながると分かるまでは、画面の「決定」・「保存」を押せない (<see cref="IsVerified"/>。入力を直したら、また確認が要る)。
/// パスワードは、入力したときだけ保存する (空のままなら、登録済みのものを変えない)。保存・接続の確認の結果は、<see cref="Error"/> / <see cref="Success"/> に出す。
/// サーバーの証明書は、画面の項目にしない。証明書を信頼できないサーバー (自己署名など)のときだけ、初めての接続で 1 回、「接続する」かを聞き
/// (<see cref="NeedsCertificateConsent"/>)、答えた内容を、サーバーとセットで覚える (サーバーを変えたときは、また聞く)。
/// </remarks>
public sealed partial class DatabaseConnectionViewModel : ObservableObject
{
    /// <summary>設定を読めなかったときのエラー</summary>
    private const string ReadOnlyMessage = "設定を読み込めなかったため、変更を保存できませんでした。";

    /// <summary>接続の設定の読み書き</summary>
    private readonly DatabaseSettingsService _settings;

    /// <summary>証明書を信頼して接続するか (<see cref="_trustedServer"/> のサーバーのときだけ有効)</summary>
    private bool _trustServerCertificate;

    /// <summary>証明書を信頼すると答えたサーバー</summary>
    private string _trustedServer;

    /// <summary>証明書についての答えを待っているときの、答え。待っていなければ null</summary>
    private TaskCompletionSource<bool>? _decision;

    /// <summary>ViewModel を作り、保存済みの設定を読み込む</summary>
    /// <param name="settings">接続の設定の読み書き</param>
    public DatabaseConnectionViewModel(DatabaseSettingsService settings)
    {
        _settings = settings;
        IsEditable = !settings.IsReadOnly;

        var value = settings.Load();
        Server = value.Server;
        DatabaseName = value.Name;
        UserName = value.UserName;
        _trustServerCertificate = value.TrustServerCertificate;
        _trustedServer = value.Server;

        try
        {
            HasPassword = !string.IsNullOrEmpty(settings.GetPassword());
        }
        catch (SecretStoreException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>入力欄を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>サーバー (<c>ホスト名</c>・<c>ホスト名\インスタンス名</c>・<c>ホスト名,ポート</c>)</summary>
    [ObservableProperty]
    public partial string Server { get; set; } = "";

    /// <summary>データベース名</summary>
    [ObservableProperty]
    public partial string DatabaseName { get; set; } = "";

    /// <summary>ユーザー名</summary>
    [ObservableProperty]
    public partial string UserName { get; set; } = "";

    /// <summary>入力したパスワード (保存したら空に戻す)</summary>
    [ObservableProperty]
    public partial string Password { get; set; } = "";

    /// <summary>パスワードが登録済みか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordHeader))]
    public partial bool HasPassword { get; private set; }

    /// <summary>パスワードの欄の見出し (登録済みなら、変えるときだけ入力することを添える)</summary>
    public string PasswordHeader => HasPassword ? "パスワード (登録済み。変えるときだけ入力)" : "パスワード";

    /// <summary>今の入力で、つながると確認できているか</summary>
    /// <remarks>サーバー・データベース名・ユーザー名・パスワードを直すと、false に戻る。画面は、これが true になるまで、DB の「決定」・「保存」を押せなくする。</remarks>
    [ObservableProperty]
    public partial bool IsVerified { get; private set; }

    /// <summary>接続の確認・保存の最中か</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; private set; }

    /// <summary>接続の確認・保存の最中ではないか (「接続を確認」を押せる)</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>サーバーの証明書について、「接続する」かを聞いているか</summary>
    [ObservableProperty]
    public partial bool NeedsCertificateConsent { get; private set; }

    /// <summary>画面に出すエラー (保存・接続の確認に失敗したとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>画面に出す成功のお知らせ (保存・接続の確認ができたとき)</summary>
    public ErrorState Success { get; } = new();

    /// <summary>今のサーバーに対して、証明書を信頼する答えを、覚えているか</summary>
    private bool TrustsServerNow => _trustServerCertificate && Server.Trim() == _trustedServer;

    /// <summary>入力した内容を、保存先の種類を添えて保存する</summary>
    /// <param name="mode">保存する保存先の種類</param>
    /// <returns>すべて保存できたら true。入力が足りない・つながらない・保存できなかったときは、エラーを出して false</returns>
    /// <remarks>
    /// 保存先が DB のときは、サーバーとデータベース名が要る。「接続を確認」で、つながると確認できていなければ、保存の前に、つながることを確かめる (つながらない設定を保存しないため)。
    /// 証明書を聞かれて「やめる」と答えたときも false。パスワードは、入力があるときだけ保存する。
    /// </remarks>
    public async Task<bool> SaveAsync(DatabaseMode mode)
    {
        if (IsBusy)
        {
            return false;
        }

        Error.Clear();
        Success.Clear();
        if (mode == DatabaseMode.SqlServer && (Server.Trim().Length == 0 || DatabaseName.Trim().Length == 0))
        {
            Error.Show("サーバーとデータベース名を入力してください。");
            return false;
        }

        IsBusy = true;
        try
        {
            if (mode == DatabaseMode.SqlServer && !IsVerified && !await TryConnectAsync())
            {
                return false;
            }

            if (!await _settings.SaveAsync(ToSettings(mode)))
            {
                Error.Show(ReadOnlyMessage);
                return false;
            }

            if (Password.Length > 0)
            {
                _settings.SetPassword(Password);

                // 保存したパスワードの入力欄を空に戻しても、確認済みの状態は、そのまま保つ
                var verified = IsVerified;
                Password = "";
                IsVerified = verified;
                HasPassword = true;
            }
            return true;
        }
        catch (Exception ex) when (ex is DataFileException or SecretStoreException)
        {
            Error.Show(ex.Message);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>入力した内容で、DB につながるか確かめる</summary>
    /// <returns>確認の完了を表すタスク</returns>
    /// <remarks>保存はしない。パスワードを入力していなければ、登録済みのものを使う。足りない設定・接続の失敗は、画面に出す。</remarks>
    [RelayCommand]
    private async Task TestAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Error.Clear();
        Success.Clear();
        IsBusy = true;
        try
        {
            if (await TryConnectAsync())
            {
                Success.Show("接続できました。");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>証明書について、「接続する」と答える</summary>
    /// <remarks>今のサーバーに対して、証明書を信頼する答えを覚えて、接続をやり直させる (保存するのは、「保存」・「決定」のとき)。</remarks>
    [RelayCommand]
    private void AcceptCertificate()
    {
        _trustServerCertificate = true;
        _trustedServer = Server.Trim();
        _decision?.TrySetResult(true);
    }

    /// <summary>証明書について、「やめる」と答える</summary>
    [RelayCommand]
    private void DeclineCertificate() => _decision?.TrySetResult(false);

    /// <summary>入力した内容で接続を試す。失敗は画面に出す</summary>
    /// <returns>つながったら true。つながらない・「やめる」と答えたときは false</returns>
    /// <remarks>証明書を信頼できないとき (まだ答えていないサーバー)だけ、「接続する」かを聞き、「接続する」ならやり直す。</remarks>
    private async Task<bool> TryConnectAsync()
    {
        while (true)
        {
            try
            {
                var password = Password.Length > 0 ? Password : _settings.GetPassword();
                await SqlServerConnectionFactoryBuilder.TestConnectionAsync(ToSettings(DatabaseMode.SqlServer), password);
                IsVerified = true;
                return true;
            }
            catch (SqlServerConnectionException ex) when (ex.IsUntrustedCertificate && !TrustsServerNow)
            {
                if (!await AskCertificateAsync())
                {
                    return false;
                }
            }
            catch (Exception ex) when (ex is DatabaseSettingsException or SqlServerConnectionException or SecretStoreException)
            {
                Error.Show(ex.Message);
                return false;
            }
        }
    }

    /// <summary>証明書について聞いて、答えを待つ</summary>
    /// <returns>「接続する」なら true。「やめる」なら false</returns>
    private async Task<bool> AskCertificateAsync()
    {
        _decision = new TaskCompletionSource<bool>();
        NeedsCertificateConsent = true;
        try
        {
            return await _decision.Task;
        }
        finally
        {
            NeedsCertificateConsent = false;
            _decision = null;
        }
    }

    /// <summary>サーバーを直したら、確認済みを取り消す</summary>
    /// <param name="value">直したあとのサーバー</param>
    partial void OnServerChanged(string value) => IsVerified = false;

    /// <summary>データベース名を直したら、確認済みを取り消す</summary>
    /// <param name="value">直したあとのデータベース名</param>
    partial void OnDatabaseNameChanged(string value) => IsVerified = false;

    /// <summary>ユーザー名を直したら、確認済みを取り消す</summary>
    /// <param name="value">直したあとのユーザー名</param>
    partial void OnUserNameChanged(string value) => IsVerified = false;

    /// <summary>パスワードを直したら、確認済みを取り消す</summary>
    /// <param name="value">直したあとのパスワード</param>
    partial void OnPasswordChanged(string value) => IsVerified = false;

    /// <summary>入力欄の内容を、接続の設定にする</summary>
    /// <param name="mode">保存先の種類</param>
    /// <returns>接続の設定 (パスワードを除く)</returns>
    private DatabaseSettings ToSettings(DatabaseMode mode) => new()
    {
        Mode = mode,
        Server = Server.Trim(),
        Name = DatabaseName.Trim(),
        Authentication = DatabaseAuthentication.Sql,
        UserName = UserName.Trim(),
        TrustServerCertificate = TrustsServerNow,
    };
}
