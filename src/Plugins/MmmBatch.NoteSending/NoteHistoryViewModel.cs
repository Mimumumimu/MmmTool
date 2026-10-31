using MmmBatch.NoteSending.Core;
using MmmBatch.Sending;
using MmmBatch.Sending.Core;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch.NoteSending;

/// <summary>ノートの送信の履歴の画面 (起動してから送った、ノートの記録を並べる)</summary>
/// <param name="history">起動してから送った分の記録</param>
/// <param name="service">ノートの送信の処理</param>
/// <param name="dialogs">接続の設定の画面を開く</param>
/// <param name="monitor">定期の監視</param>
public sealed class NoteHistoryViewModel(ISendHistory history, NoteSendService service, IConnectionSettingsDialogService dialogs, BatchMonitor monitor)
    : SendHistoryViewModel(NoteSendContent.Source, history, service, dialogs, monitor);
