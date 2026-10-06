using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Db.SqlServer.Components.Connections;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;

namespace MmmTool.Features.Database.Connection;

/// <summary>
/// DB への接続の入力欄 (サーバー名・データベース名・ログインの方式・ユーザー名・パスワード・証明書) の ViewModel。
/// </summary>
/// <remarks>
/// 設定ページの「保存先」と、初回の保存先の選択の画面が、同じ入力欄 (<see cref="DatabaseConnectionForm"/>)として使う。
/// パスワードは、入力したときだけ保存する (空のままなら、登録済みのものを変えない)。保存・接続の確認の結果は、<see cref="Error"/> / <see cref="Success"/> に出す。
/// </remarks>
public sealed partial class DatabaseConnectionViewModel : ObservableObject
{
    /// <summary>設定を読めなかったときのエラー</summary>
    private const string ReadOnlyMessage = "設定を読み込めなかったため、変更を保存できませんでした。";

    /// <summary>接続の設定の読み書き</summary>
    private readonly DatabaseSettingsService _settings;

    /// <summary>ViewModel を作り、保存済みの設定を読み込む</summary>
    /// <param name="settings">接続の設定の読み書き</param>
    public DatabaseConnectionViewModel(DatabaseSettingsService settings)
    {
        _settings = settings;
        IsEditable = !settings.IsReadOnly;
        Authentications =
        [
            new DatabaseAuthenticationOption(DatabaseAuthentication.Sql, "ユーザー名とパスワード"),
            new DatabaseAuthenticationOption(DatabaseAuthentication.Windows, "Windows 認証 (今の Windows ユーザー)"),
        ];

        var value = settings.Load();
        Server = value.Server;
        DatabaseName = value.Name;
        UserName = value.UserName;
        TrustServerCertificate = value.TrustServerCertificate;
        SelectedAuthentication = Authentications.First(option => option.Value == value.Authentication);

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

    /// <summary>ログインの方式の選択肢</summary>
    public IReadOnlyList<DatabaseAuthenticationOption> Authentications { get; }

    /// <summary>サーバー名</summary>
    [ObservableProperty]
    public partial string Server { get; set; } = "";

    /// <summary>データベース名</summary>
    [ObservableProperty]
    public partial string DatabaseName { get; set; } = "";

    /// <summary>選んでいるログインの方式</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSqlLogin))]
    public partial DatabaseAuthenticationOption? SelectedAuthentication { get; set; }

    /// <summary>ユーザー名とパスワードのログインか (ユーザー名・パスワードの欄を出す)</summary>
    public bool IsSqlLogin => SelectedAuthentication is not { Value: DatabaseAuthentication.Windows };

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

    /// <summary>サーバーの証明書を検証せずに信頼するか</summary>
    [ObservableProperty]
    public partial bool TrustServerCertificate { get; set; }

    /// <summary>接続を確認している最中か</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotTesting))]
    public partial bool IsTesting { get; private set; }

    /// <summary>接続を確認していないか (「接続を確認」を押せる)</summary>
    public bool IsNotTesting => !IsTesting;

    /// <summary>画面に出すエラー (保存・接続の確認に失敗したとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>画面に出す成功のお知らせ (保存・接続の確認ができたとき)</summary>
    public ErrorState Success { get; } = new();

    /// <summary>入力した内容を、保存先の種類を添えて保存する</summary>
    /// <param name="mode">保存する保存先の種類</param>
    /// <returns>すべて保存できたら true。入力が足りない・保存できなかったときは、エラーを出して false</returns>
    /// <remarks>保存先が DB のときは、サーバー名とデータベース名が要る。パスワードは、入力があるときだけ保存する。</remarks>
    public async Task<bool> SaveAsync(DatabaseMode mode)
    {
        Error.Clear();
        Success.Clear();

        var value = ToSettings(mode);
        if (mode == DatabaseMode.SqlServer && (value.Server.Length == 0 || value.Name.Length == 0))
        {
            Error.Show("サーバー名とデータベース名を入力してください。");
            return false;
        }

        try
        {
            if (!await _settings.SaveAsync(value))
            {
                Error.Show(ReadOnlyMessage);
                return false;
            }

            if (value.Authentication == DatabaseAuthentication.Sql && Password.Length > 0)
            {
                _settings.SetPassword(Password);
                Password = "";
                HasPassword = true;
            }
            return true;
        }
        catch (Exception ex) when (ex is DataFileException or SecretStoreException)
        {
            Error.Show(ex.Message);
            return false;
        }
    }

    /// <summary>入力した内容で、DB に接続できるか確認する</summary>
    /// <returns>確認の完了を表すタスク</returns>
    /// <remarks>保存はしない。パスワードを入力していなければ、登録済みのものを使う。足りない設定・接続の失敗は、画面に出す。</remarks>
    [RelayCommand]
    private async Task TestAsync()
    {
        if (IsTesting)
        {
            return;
        }

        Error.Clear();
        Success.Clear();
        IsTesting = true;
        try
        {
            var value = ToSettings(DatabaseMode.SqlServer);
            var password = value.Authentication != DatabaseAuthentication.Sql ? null : Password.Length > 0 ? Password : _settings.GetPassword();
            await SqlServerConnectionFactoryBuilder.TestConnectionAsync(value, password);
            Success.Show("接続できました。");
        }
        catch (Exception ex) when (ex is DatabaseSettingsException or SqlServerConnectionException or SecretStoreException)
        {
            Error.Show(ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>入力欄の内容を、接続の設定にする</summary>
    /// <param name="mode">保存先の種類</param>
    /// <returns>接続の設定 (パスワードを除く)</returns>
    private DatabaseSettings ToSettings(DatabaseMode mode) => new()
    {
        Mode = mode,
        Server = Server.Trim(),
        Name = DatabaseName.Trim(),
        Authentication = SelectedAuthentication?.Value ?? DatabaseAuthentication.Sql,
        UserName = UserName.Trim(),
        TrustServerCertificate = TrustServerCertificate,
    };
}
