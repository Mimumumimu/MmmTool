using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmBatch.MessageSending.Core;
using MmmBatch.Sending;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.WinUI.Connection;

namespace MmmBatch.MessageSending;

/// <summary>
/// 最近の依頼の画面。依頼を、日付や状態にかかわらず、新しい順に並べ、状態を見せて、右クリックで「再送」「今すぐ送る」ができる。
/// </summary>
/// <remarks>
/// 送信の状況が変わったときに読み直す。新しい依頼は、仕事が数秒以内に拾って送るので、その結果 (送った・失敗した)で読み直される
/// (数秒おきの実行のたびには読み直さない。DB への読み込みを増やさないため)。
/// 日付で絞らない (日をまたいだ失敗を、再送できなくなるため)。
/// </remarks>
public sealed partial class MessageRecentViewModel : SendViewModelBase
{
    /// <summary>メッセージの送信の処理</summary>
    private readonly MessageSendService _service;

    /// <summary>ViewModel を作る</summary>
    /// <param name="service">メッセージの送信の処理</param>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    /// <param name="monitor">毎分の監視</param>
    public MessageRecentViewModel(MessageSendService service, IConnectionSettingsDialogService dialogs, BatchMonitor monitor)
        : base(service, dialogs, monitor)
    {
        _service = service;
    }

    /// <summary>一覧の行 (新しい順)</summary>
    public ObservableCollection<MessageRecentItemViewModel> Items { get; } = [];

    /// <summary>一覧が空か (読み込みが済んで、1 件も無い)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        var version = NextVersion();
        IReadOnlyList<MessageSendTarget> loaded;
        try
        {
            loaded = await _service.GetRecentAsync();
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

        // 変わった行だけ差し替える (全部を作り直すとスクロール位置が先頭へ戻るため)
        for (var i = 0; i < loaded.Count; i++)
        {
            if (i >= Items.Count)
            {
                Items.Add(new MessageRecentItemViewModel(loaded[i]));
            }
            else if (!Items[i].Source.Equals(loaded[i]))
            {
                Items[i] = new MessageRecentItemViewModel(loaded[i]);
            }
        }
        while (Items.Count > loaded.Count)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        IsEmpty = Items.Count == 0;
        Error.Set(Job.LastError);
    }

    /// <summary>今すぐ 1 回送る (送信済み・失敗の上限に達したものも送れる)</summary>
    /// <param name="item">送る行</param>
    /// <returns>送信と読み直しの完了を表すタスク</returns>
    /// <remarks>送れなかったときは、その行が「失敗」になる。DB の失敗 (接続できない・表が無い)は、エラーに出す。</remarks>
    [RelayCommand]
    private async Task ResendAsync(MessageRecentItemViewModel item)
    {
        try
        {
            await Monitor.RunExclusiveAsync(cancellationToken => _service.ResendAsync(item.Source, cancellationToken));
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return;
        }

        await RefreshAsync();
    }
}
