using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending;

/// <summary>
/// 送信の履歴の画面。新しい順に、最大 <see cref="MaxCount"/> 件を並べる。
/// </summary>
/// <remarks>送信の状況が変わったときだけ読み直す (毎分は読み直さない。スクロール位置を保つため)。</remarks>
public sealed partial class SendHistoryViewModel : SendViewModelBase
{
    /// <summary>一覧に出す最大の件数</summary>
    public const int MaxCount = 100;

    /// <summary>送信の状況の保存先</summary>
    private readonly IReminderSendStatusRepository _statuses;

    /// <summary>ViewModel を作る</summary>
    /// <param name="statuses">送信の状況の保存先</param>
    /// <param name="service">送信の処理</param>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    /// <param name="monitor">毎分の送信の監視</param>
    public SendHistoryViewModel(IReminderSendStatusRepository statuses, ReminderSendService service, ISendingDialogService dialogs, SendMonitor monitor)
        : base(service, dialogs, monitor)
    {
        _statuses = statuses;
    }

    /// <summary>一覧の行 (新しい順)</summary>
    public ObservableCollection<SendHistoryItemViewModel> Items { get; } = [];

    /// <summary>一覧が空か (読み込みが済んで、1 件も無い)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        var version = NextVersion();
        IReadOnlyList<SendHistoryItem> loaded;
        try
        {
            loaded = await _statuses.GetRecentAsync(MaxCount);
        }
        catch (DataFileException ex)
        {
            if (IsCurrent(version))
            {
                Error.Show(ex.Message);
            }
            return;
        }

        if (!IsCurrent(version))
        {
            return;
        }

        Items.Clear();
        foreach (var item in loaded)
        {
            Items.Add(new SendHistoryItemViewModel(item));
        }
        IsEmpty = Items.Count == 0;
        Error.Set(Service.LastError);
    }
}
