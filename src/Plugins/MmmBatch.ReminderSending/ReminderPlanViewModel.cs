using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Storage;

namespace MmmBatch.ReminderSending;

/// <summary>
/// 今日の送信予定の画面。今日送るリマインダーを時刻順に並べ、状態を見せて、右クリックで「再送」「今すぐ送る」ができる。
/// </summary>
/// <remarks>
/// 送信の状況が変わったとき、と、毎分の実行が終わったときに読み直す (リマインダー・送信設定の編集を、すぐ反映するため)。
/// 今日の分でないもの・削除したもの・送信先なしは出さない (保存した時点で時刻が過ぎていて、今日は送らないものは、「対象外 (過去)」で出す)。
/// </remarks>
public sealed partial class ReminderPlanViewModel : SendViewModelBase
{
    /// <summary>リマインダーの送信の処理</summary>
    private readonly ReminderSendService _service;

    /// <summary>現在時刻の提供元</summary>
    private readonly TimeProvider _time;

    /// <summary>ViewModel を作る</summary>
    /// <param name="service">リマインダーの送信の処理</param>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    /// <param name="monitor">毎分の監視</param>
    /// <param name="time">現在時刻の提供元</param>
    public ReminderPlanViewModel(ReminderSendService service, ISendingDialogService dialogs, BatchMonitor monitor, TimeProvider time)
        : base(service, dialogs, monitor, refreshEveryRun: true)
    {
        _service = service;
        _time = time;
    }

    /// <summary>一覧の行 (時刻順)</summary>
    public ObservableCollection<ReminderPlanItemViewModel> Items { get; } = [];

    /// <summary>一覧が空か (読み込みが済んで、1 件も無い)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        var version = NextVersion();
        IReadOnlyList<ReminderSendPlanItem> loaded;
        try
        {
            loaded = await _service.GetTodayPlanAsync(_time.GetLocalNow().DateTime);
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
                Items.Add(new ReminderPlanItemViewModel(loaded[i]));
            }
            else if (!Items[i].Source.Equals(loaded[i]))
            {
                Items[i] = new ReminderPlanItemViewModel(loaded[i]);
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
    private async Task ResendAsync(ReminderPlanItemViewModel item)
    {
        try
        {
            await Monitor.RunExclusiveAsync(cancellationToken
                => _service.ResendAsync(item.Source.Target, _time.GetLocalNow().DateTime, cancellationToken));
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return;
        }

        await RefreshAsync();
    }
}
