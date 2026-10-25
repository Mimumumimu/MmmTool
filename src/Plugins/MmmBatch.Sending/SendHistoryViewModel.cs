using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmBatch.Sending.Core;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch.Sending;

/// <summary>
/// 送信の履歴の画面。このアプリを起動してから送った 1 回ごとの記録 (メモリ上)を、機能ごとに、新しい順に、最大 <see cref="MaxCount"/> 件並べる。
/// </summary>
/// <remarks>
/// 起動時は空 (揮発性。ログのファイルは読まない)。機能の仕事の結果が変わったとき (送ったとき)に読み直す。
/// 機能ごとに、この型を継承して、機能のキー (<see cref="SendLogEntry.Source"/>)と仕事を渡す。
/// </remarks>
public partial class SendHistoryViewModel : SendViewModelBase
{
    /// <summary>一覧に出す最大の件数</summary>
    public const int MaxCount = 100;

    /// <summary>起動してから送った分の記録</summary>
    private readonly ISendHistory _history;

    /// <summary>この履歴の機能のキー (<see cref="SendLogEntry.Source"/>)</summary>
    private readonly string _source;

    /// <summary>ViewModel を作る</summary>
    /// <param name="source">この履歴の機能のキー (<see cref="SendLogEntry.Source"/>)。この機能の行だけを出す</param>
    /// <param name="history">起動してから送った分の記録</param>
    /// <param name="job">この履歴の機能の仕事</param>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    /// <param name="monitor">毎分の監視</param>
    protected SendHistoryViewModel(string source, ISendHistory history, IBatchJob job, IConnectionSettingsDialogService dialogs, BatchMonitor monitor)
        : base(job, dialogs, monitor)
    {
        _source = source;
        _history = history;
    }

    /// <summary>一覧の行 (新しい順)</summary>
    public ObservableCollection<SendHistoryItemViewModel> Items { get; } = [];

    /// <summary>一覧が空か (まだ 1 件も送っていない)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <inheritdoc />
    public override Task RefreshAsync()
    {
        Items.Clear();
        foreach (var entry in _history.GetRecent(_source, MaxCount))
        {
            Items.Add(new SendHistoryItemViewModel(entry));
        }

        IsEmpty = Items.Count == 0;
        Error.Set(Job.LastError);
        return Task.CompletedTask;
    }
}
