using MmmBatch.MessageSending.Core;
using MmmBatch.Sending;
using MmmBatch.Sending.Core;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch.MessageSending;

/// <summary>メッセージの送信の履歴の画面 (起動してから送った、メッセージの記録を並べる)</summary>
/// <param name="history">起動してから送った分の記録</param>
/// <param name="service">メッセージの送信の処理</param>
/// <param name="dialogs">接続の設定の画面を開く</param>
/// <param name="monitor">毎分の監視</param>
public sealed class MessageHistoryViewModel(ISendHistory history, MessageSendService service, IConnectionSettingsDialogService dialogs, BatchMonitor monitor)
    : SendHistoryViewModel(MessageSendContent.Source, history, service, dialogs, monitor);
