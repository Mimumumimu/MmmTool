using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.WinUI.Connection;
using MmmTool.Shell;

namespace MmmTool.Features.Database.Edit;

/// <summary>
/// 保存先 (ローカル / DB)と、DB への接続を編集する画面の ViewModel。
/// </summary>
/// <remarks>
/// 保存するのは「保存」を押したときだけ。DB のときは、「接続を確認」で、つながると確認できるまで、「保存」を押せない。保存先は起動時に決まって使い始めるので、保存先や接続が変わったときは、反映に再起動が要ることを確認して、
/// 「今すぐ終了」ならアプリを終了する (起動し直すのは利用者)。保存できたら <see cref="CloseRequested"/> で、画面を閉じてよいことを知らせる。
/// </remarks>
public sealed partial class DatabaseEditViewModel : ObservableObject
{
    /// <summary>接続の設定の読み書き</summary>
    private readonly DatabaseSettingsService _settings;

    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _dialogs;

    /// <summary>アプリの終了を頼む口</summary>
    private readonly AppExitService _exit;

    /// <summary>ViewModel を作り、保存済みの保存先を読み込む</summary>
    /// <param name="connection">接続の入力欄</param>
    /// <param name="settings">接続の設定の読み書き</param>
    /// <param name="dialogs">確認ダイアログを開く</param>
    /// <param name="exit">アプリの終了を頼む口</param>
    public DatabaseEditViewModel(DatabaseConnectionViewModel connection, DatabaseSettingsService settings, IDialogService dialogs, AppExitService exit)
    {
        Connection = connection;
        _settings = settings;
        _dialogs = dialogs;
        _exit = exit;
        connection.PropertyChanged += OnConnectionPropertyChanged;
        IsEditable = !settings.IsReadOnly;
        IsSqlServer = settings.Load().Mode == DatabaseMode.SqlServer;
    }

    /// <summary>画面を閉じてよい</summary>
    public event EventHandler? CloseRequested;

    /// <summary>接続の入力欄 (保存先が DB のときに出す)</summary>
    public DatabaseConnectionViewModel Connection { get; }

    /// <summary>設定を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>DB を選んでいるか (オフならローカル)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocal), nameof(NeedsVerification))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSqlServer { get; set; }

    /// <summary>ローカルを選んでいるか</summary>
    public bool IsLocal => !IsSqlServer;

    /// <summary>「接続を確認」が済んでいないため、「保存」を押せないか (DB を選んでいて、つながると確認できていない)</summary>
    public bool NeedsVerification => IsSqlServer && !Connection.IsVerified;

    /// <summary>「保存」を押せるか (設定を変更でき、ローカルか、DB でつながると確認できている)</summary>
    private bool CanSave() => IsEditable && !NeedsVerification;

    /// <summary>保存先と接続の設定を保存して、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>
    /// 保存できなかったとき (入力が足りない・設定ファイルを読めなかった)は、入力欄のエラーに出して閉じない。
    /// 保存先に関わる設定が変わったときは、再起動が要ることを確認する。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var before = _settings.Load();
        var passwordEntered = Connection.Password.Length > 0;
        if (!await Connection.SaveAsync(IsSqlServer ? DatabaseMode.SqlServer : DatabaseMode.Json))
        {
            return;
        }

        var exitNow = AffectsStorage(before, _settings.Load(), passwordEntered)
            && await _dialogs.ConfirmAsync("保存先の設定を変更しました", "反映するには、アプリの再起動が必要です。\n今すぐ終了しますか？", "今すぐ終了", "あとで");

        // 終了を頼むのは、この画面を閉じたあと (終了の処理が、開いたままの画面と重ならないようにする)
        CloseRequested?.Invoke(this, EventArgs.Empty);
        if (exitNow)
        {
            _exit.RequestExit();
        }
    }

    /// <summary>保存せずに閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>接続の入力欄の確認の状態が変わったら、「保存」を押せるかを更新する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DatabaseConnectionViewModel.IsVerified))
        {
            OnPropertyChanged(nameof(NeedsVerification));
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>保存先の使い方が変わる変更か</summary>
    /// <param name="before">保存前の設定</param>
    /// <param name="after">保存後の設定</param>
    /// <param name="passwordEntered">パスワードを入力して保存したか</param>
    /// <returns>保存先の種類が変わった、または DB のまま接続が変わったなら true (ローカルのままなら、DB の入力欄の変更は影響しない)</returns>
    private static bool AffectsStorage(DatabaseSettings before, DatabaseSettings after, bool passwordEntered)
        => before.Mode != after.Mode || (after.Mode == DatabaseMode.SqlServer && (before != after || passwordEntered));
}
