using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Data.Connection;
using MmmTool.Features.Database.Connection;

namespace MmmTool.Features.Database.Choice;

/// <summary>
/// 初回の保存先の選択の画面の ViewModel。ローカル (この PC だけ)か DB (複数の PC で共有)かを選び、DB なら接続の設定も入れる。
/// </summary>
/// <param name="connection">接続の入力欄</param>
/// <remarks>起動時の準備より前に出す画面なので、選んだ内容は、保存すれば、そのまま最初から効く。保存できたら <see cref="CloseRequested"/> で、決定したことを知らせる。</remarks>
public sealed partial class DatabaseChoiceViewModel(DatabaseConnectionViewModel connection) : ObservableObject
{
    /// <summary>決定した (画面を閉じてよい)</summary>
    /// <remarks>保存したら true を渡す。</remarks>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>接続の入力欄 (DB を選んだときに出す)</summary>
    public DatabaseConnectionViewModel Connection { get; } = connection;

    /// <summary>DB を選んでいるか (オフならローカル)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocal))]
    public partial bool IsSqlServer { get; set; }

    /// <summary>ローカルを選んでいるか (既定)</summary>
    public bool IsLocal => !IsSqlServer;

    /// <summary>選んだ保存先を保存して、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存できなかったとき (DB の入力が足りない・設定ファイルを読めなかった)は、入力欄のエラーに出して閉じない。</remarks>
    [RelayCommand]
    private async Task DecideAsync()
    {
        if (await Connection.SaveAsync(IsSqlServer ? DatabaseMode.SqlServer : DatabaseMode.Json))
        {
            CloseRequested?.Invoke(this, true);
        }
    }
}
