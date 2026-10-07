using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Data.Connection;
using MmmTool.Features.Database.Connection;

namespace MmmTool.Features.Database.Choice;

/// <summary>
/// 初回の保存先の選択の画面の ViewModel。ローカル (この PC だけ)か DB (サーバー)かを選び、DB なら接続の設定も入れる。
/// </summary>
/// <remarks>
/// 起動時の準備より前に出す画面なので、選んだ内容は、保存すれば、そのまま最初から効く。保存できたら <see cref="CloseRequested"/> で、決定したことを知らせる。
/// DB のときは、「接続を確認」で、つながると確認できるまで、「決定」を押せない。
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

    /// <summary>決定した (画面を閉じてよい)</summary>
    /// <remarks>保存したら true を渡す。</remarks>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>接続の入力欄 (DB を選んだときに出す)</summary>
    public DatabaseConnectionViewModel Connection { get; }

    /// <summary>DB を選んでいるか (オフならローカル)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocal), nameof(NeedsVerification))]
    [NotifyCanExecuteChangedFor(nameof(DecideCommand))]
    public partial bool IsSqlServer { get; set; }

    /// <summary>ローカルを選んでいるか (既定)</summary>
    public bool IsLocal => !IsSqlServer;

    /// <summary>「接続を確認」が済んでいないため、「決定」を押せないか (DB を選んでいて、つながると確認できていない)</summary>
    public bool NeedsVerification => IsSqlServer && !Connection.IsVerified;

    /// <summary>「決定」を押せるか (ローカルか、DB でつながると確認できている)</summary>
    private bool CanDecide() => !NeedsVerification;

    /// <summary>選んだ保存先を保存して、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存できなかったとき (DB の入力が足りない・設定ファイルを読めなかった)は、入力欄のエラーに出して閉じない。</remarks>
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
