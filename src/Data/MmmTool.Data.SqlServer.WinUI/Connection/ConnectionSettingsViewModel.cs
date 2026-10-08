using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>
/// DB の接続の設定画面。入力欄 (<see cref="DatabaseConnectionViewModel"/>)に、保存とキャンセルを付ける。
/// </summary>
/// <remarks>
/// 「接続を確認」で、つながると確認できるまで、保存できない (MmmTool の保存先の画面と同じ。確認の順序が、画面の見た目で分かるようにするため)。
/// 保存できたら、覚えている接続を捨てて (<see cref="SqlServerDatabase.ResetConnection"/>)、再起動せずに新しい設定を使い、画面を閉じてもらう。
/// </remarks>
public sealed partial class ConnectionSettingsViewModel : ObservableObject
{
    /// <summary>SQL Server への入口</summary>
    private readonly SqlServerDatabase _database;

    /// <summary>ViewModel を作る</summary>
    /// <param name="connection">接続の入力欄の ViewModel</param>
    /// <param name="database">SQL Server への入口</param>
    public ConnectionSettingsViewModel(DatabaseConnectionViewModel connection, SqlServerDatabase database)
    {
        Connection = connection;
        _database = database;
        Connection.PropertyChanged += OnConnectionPropertyChanged;
    }

    /// <summary>画面を閉じてほしい</summary>
    public event EventHandler? CloseRequested;

    /// <summary>接続の入力欄の ViewModel</summary>
    public DatabaseConnectionViewModel Connection { get; }

    /// <summary>「保存」を押せない理由があるか (つながると、まだ確認できていない)</summary>
    public bool NeedsVerification => !Connection.IsVerified;

    /// <summary>保存できるか (入力欄を変更できて、つながると確認できている)</summary>
    private bool CanSave() => Connection.IsEditable && !NeedsVerification;

    /// <summary>入力した内容を保存して、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存できなかったとき (つながらない・保存できない)は、入力欄のエラーに出して、閉じない。</remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (await Connection.SaveAsync(DatabaseMode.SqlServer))
        {
            _database.ResetConnection();
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
}
