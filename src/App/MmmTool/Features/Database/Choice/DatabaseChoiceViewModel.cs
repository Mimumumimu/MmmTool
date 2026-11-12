using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmTool.Features.Database.Choice;

/// <summary>初回の保存先の選択の画面の ViewModel</summary>
/// <remarks>
/// この PC だけ。
/// ローカルか DBかを選び、DB なら接続の設定も入れる。
/// 起動時の準備より前に出す画面なので、選んだ内容は保存すれば、そのまま最初から効く。
/// 保存できたら <see cref="CloseRequested"/> で、決定したことを知らせる。
/// DB のときは「接続を確認」でつながると確認できるまで、「決定」を押せない。
/// </remarks>
public sealed partial class DatabaseChoiceViewModel : ObservableObject
{
    /// <summary>ViewModel を作る</summary>
    /// <param name="connection">接続の入力欄</param>
    public DatabaseChoiceViewModel(DatabaseConnectionViewModel connection)
    {
        Connection = connection;
        connection.PropertyChanged += OnConnectionPropertyChanged;
    }

    /// <summary>決定した</summary>
    /// <remarks>
    /// 画面を閉じてよい
    /// 保存したら true を渡す
    /// </remarks>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>接続の入力欄</summary>
    /// <remarks>DB を選んだときに出す</remarks>
    public DatabaseConnectionViewModel Connection { get; }

    /// <summary>DB を選んでいるか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocal), nameof(NeedsVerification))]
    [NotifyCanExecuteChangedFor(nameof(DecideCommand))]
    public partial bool IsSqlServer { get; set; }

    /// <summary>ローカルを選んでいるか</summary>
    public bool IsLocal => !IsSqlServer;

    /// <summary>「接続を確認」が済んでいないため、「決定」を押せないか</summary>
    /// <remarks>DB を選んでいて、つながると確認できていない。</remarks>
    public bool NeedsVerification => IsSqlServer && !Connection.IsVerified;

    /// <summary>「決定」を押せるか</summary>
    /// <remarks>ローカルか、DB でつながると確認できている。</remarks>
    private bool CanDecide() => !NeedsVerification;

    /// <summary>選んだ保存先を保存して閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存できなかったとき (DB の入力が足りない・設定ファイルを読めなかった)は入力欄のエラーに出して閉じない</remarks>
    [RelayCommand(CanExecute = nameof(CanDecide))]
    private async Task DecideAsync()
    {
        if (await Connection.SaveAsync(IsSqlServer ? DatabaseMode.SqlServer : DatabaseMode.Json))
        {
            CloseRequested?.Invoke(this, true);
        }
    }

    /// <summary>接続の入力欄の確認の状態が変わったら、「決定」を押せるかを更新する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DatabaseConnectionViewModel.IsVerified))
        {
            OnPropertyChanged(nameof(NeedsVerification));
            DecideCommand.NotifyCanExecuteChanged();
        }
    }
}
