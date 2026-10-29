using System.Diagnostics;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Storage;
using MmmTool.Notes.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.NoteSending.Core;

/// <summary>
/// 他の人が作成・更新したノートを、「ノートの通知を受け取る」がオンの人の受け取り先へ送る。1 秒おきの仕事。
/// </summary>
/// <remarks>
/// <para>
/// DB に書かない。通知の状態の表も、送った記録も持たず、基準の時刻と送る予定と送った分は、メモリだけに持つ。
/// 起動した時刻 (データベースサーバーの時計)が最初の基準で、止まっていた間の更新は、送らない。
/// 1 回の実行は、基準を <see cref="Overlap"/> 戻して、更新されたノートを読み、同じノートの同じ更新日時を 1 度しか送らないよう、送る予定に入れた分を覚えて外す。
/// </para>
/// <para>
/// 仕事は 1 つずつ動くので、1 回の実行で長く居座ると、リマインダーとメッセージの送信を止める。そのため、1 回の実行は <see cref="RunBudget"/> ほどで区切る。
/// 更新されたノートは「送る予定」に入れ、通知する相手を <see cref="RecipientPageSize"/> 人ずつ読んで、<see cref="MaxParallelSends"/> 人ずつ送る。残りは次の実行で続ける。
/// 基準は、更新されたノートを送る予定に入れ終えてから進める。途中で DB を読めなかったときは、基準を進めず、次の実行でやり直す。
/// </para>
/// <para>
/// 送れなかったときは、<see cref="SendRetryPolicy"/> の決まりで、動いている間だけ送り直す。再起動すると、送る予定と送り直しの予定は消える。
/// 気づいてほしい失敗は、人ごとではなく、ノートごとに 1 回、窓口 (<see cref="BatchAlertService"/>)へまとめて報告する。
/// MmmBatch を 2 つ動かすと、二重に送る。行を取る条件が無いため。Web 版通知を送る MmmBatch は 1 つだけ、という決まりと同じ。
/// </para>
/// </remarks>
/// <param name="source">更新されたノートと、通知する相手の読み出し口</param>
/// <param name="notifiers">外部へ送る口 (送信先の区分ごと)</param>
/// <param name="log">送信のログ (送った 1 回ごとの記録)</param>
/// <param name="alerts">気づいてほしい失敗の窓口</param>
/// <param name="time">現在時刻の提供元</param>
public sealed class NoteSendService(INoteSendSource source, IEnumerable<INotifier> notifiers, ISendLog log, BatchAlertService alerts, TimeProvider time)
    : IBatchJob
{
    /// <summary>更新されたノートを読む間隔。メッセージと同じ</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>確定が遅れた書き込みを取りこぼさないために、基準から戻して読む時間</summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromSeconds(10);

    /// <summary>1 回の実行で送る時間の目安。超えたら、残りは次の実行で続ける。1 つの送りが長引く分は、送り口のタイムアウトまで超えうる</summary>
    public static readonly TimeSpan RunBudget = TimeSpan.FromSeconds(1);

    /// <summary>1 度に読む、通知する相手の人数</summary>
    public const int RecipientPageSize = 200;

    /// <summary>同時に送る数</summary>
    public const int MaxParallelSends = 8;

    /// <summary>覚えておく、送り直しの予定の最大の数。送れない送信先が多いときに、メモリを増やし続けないため</summary>
    public const int MaxRetries = 500;

    /// <summary>覚えた「送る予定に入れた分」を捨てる古さ。戻して読む範囲より十分に長い</summary>
    private static readonly TimeSpan SeenKeep = TimeSpan.FromMinutes(5);

    /// <summary>区分ごとの、外部へ送る口</summary>
    private readonly Dictionary<NotificationChannelKind, INotifier> _notifiers = notifiers.ToDictionary(notifier => notifier.Kind);

    /// <summary>送る予定に入れた分 (ノートの番号 → 入れた更新日時)</summary>
    private readonly Dictionary<int, DateTimeOffset> _seen = [];

    /// <summary>送る予定。ノートごと。先頭から順に送る</summary>
    private readonly Queue<FanOut> _pending = new();

    /// <summary>送り直しの予定。再起動で消える</summary>
    private readonly List<Retry> _retries = [];

    /// <summary>送り直しの予定を守るロック</summary>
    private readonly Lock _retryGate = new();

    /// <summary>始めたときのデータベースサーバーの時刻。これ以前の更新は、送らない。始める前は null</summary>
    private DateTimeOffset? _floor;

    /// <summary>次に読む基準</summary>
    private DateTimeOffset _cursor;

    /// <inheritdoc />
    public string Name => "ノートの送信";

    /// <inheritdoc />
    /// <remarks>メッセージと同じく、更新をすぐ送るため、1 秒おきに読む。読むのは、更新日時の索引で絞った行だけで軽い。</remarks>
    public TimeSpan Interval => PollInterval;

    /// <inheritdoc />
    /// <remarks>任意のスレッドから発火する。</remarks>
    public event EventHandler? Changed;

    /// <inheritdoc />
    /// <remarks>任意のスレッドから発火する。</remarks>
    public event EventHandler? Ran;

    /// <inheritdoc />
    public string? LastError { get; private set; }

    /// <summary>更新されたノートを送る予定に入れ、予定の続きを、時間の目安の範囲で送る</summary>
    /// <param name="now">今の日時。ローカルの時刻で、終了日が今日以降かを決める今日に使う</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    /// <remarks>DB の読み込みの失敗は、<see cref="LastError"/> に残して、その回を終える。送る予定は残るので、次の周期に、続きから試す。</remarks>
    public async Task RunAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var changed = false;
        try
        {
            var today = DateOnly.FromDateTime(now);
            if (_floor is null)
            {
                // 最初の 1 回は、基準にするデータベースサーバーの時刻だけを受け取る。始める前の更新は、送らない
                var start = (await source.GetChangesAsync(null, today, cancellationToken).ConfigureAwait(false)).ServerNow;
                _floor = start;
                _cursor = start;
            }
            else
            {
                await EnqueueChangesAsync(today, cancellationToken).ConfigureAwait(false);
                changed |= await SendDueRetriesAsync(watch, cancellationToken).ConfigureAwait(false);
                changed |= await SendPendingAsync(watch, cancellationToken).ConfigureAwait(false);
            }

            changed |= SetLastError(null);
        }
        catch (DataFileException ex)
        {
            var errorChanged = SetLastError(ex.Message);
            changed |= errorChanged;
            if (errorChanged)
            {
                alerts.Raise(ex.Message);
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        Ran?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>更新されたノートを読んで、送る予定に入れる。基準は、全部入れ終えてから進める</summary>
    /// <param name="today">今日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>入れ終えたことを表すタスク</returns>
    /// <exception cref="DataFileException">読めなかった。入れ終えた分は予定に残り、基準は進めない。</exception>
    private async Task EnqueueChangesAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var previous = _cursor;
        var read = await source.GetChangesAsync(previous - Overlap, today, cancellationToken).ConfigureAwait(false);
        foreach (var change in read.Changes.Where(change => change.UpdatedAt > _floor && !IsSeen(change)))
        {
            var editorName = await source.GetUserNameAsync(change.EditorUserId, cancellationToken).ConfigureAwait(false);
            _seen[change.Id] = change.UpdatedAt;
            _pending.Enqueue(new FanOut(change, editorName, IsCreated: change.CreatedAt > previous));
        }

        _cursor = read.ServerNow > previous ? read.ServerNow : previous;
        Prune();
    }

    /// <summary>この更新を、すでに送る予定に入れたか</summary>
    /// <param name="change">更新されたノート</param>
    /// <returns>同じノートの、同じかそれより新しい更新日時を、入れ済みなら true</returns>
    private bool IsSeen(NoteChange change) => _seen.TryGetValue(change.Id, out var seen) && seen >= change.UpdatedAt;

    /// <summary>基準より十分に古い「入れた分」を捨てる (メモリを増やし続けないため)</summary>
    private void Prune()
    {
        foreach (var id in _seen.Where(pair => pair.Value < _cursor - SeenKeep).Select(pair => pair.Key).ToList())
        {
            _seen.Remove(id);
        }
    }

    /// <summary>送る予定を、先頭から、時間の目安の範囲で送る</summary>
    /// <param name="watch">この実行を始めてからの時間</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送ろうとしたら true</returns>
    /// <exception cref="DataFileException">通知する相手を読めなかった。送る予定は残るので、次の実行で続きから試す。</exception>
    private async Task<bool> SendPendingAsync(Stopwatch watch, CancellationToken cancellationToken)
    {
        var any = false;
        while (_pending.Count > 0 && watch.Elapsed < RunBudget)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fan = _pending.Peek();
            if (fan.Index >= fan.Page.Count)
            {
                var page = await source.GetRecipientsAsync(fan.LastUserId, fan.Change.EditorUserId, RecipientPageSize, cancellationToken).ConfigureAwait(false);
                if (page.Count == 0)
                {
                    _pending.Dequeue();
                    RaiseSummary(fan);
                    any = true;
                    continue;
                }

                fan.Page = page;
                fan.Index = 0;
                fan.LastUserId = page[^1].UserId;
            }

            var group = fan.Page.Skip(fan.Index).Take(MaxParallelSends).ToList();
            var errors = await Task.WhenAll(group.Select(recipient => SendAsync(new Retry(fan.Change, fan.EditorName, fan.IsCreated, recipient, Attempts: 0, DateTimeOffset.MinValue), cancellationToken))).ConfigureAwait(false);
            fan.Index += group.Count;
            fan.Failures += errors.Count(error => error.Length > 0);
            fan.FirstError = fan.FirstError.Length > 0 ? fan.FirstError : errors.FirstOrDefault(error => error.Length > 0) ?? "";
            any = true;
        }

        return any;
    }

    /// <summary>ノート 1 件を送り終えたとき、送れなかった人がいれば、まとめて 1 回報告する</summary>
    /// <param name="fan">送り終えたノートの予定</param>
    private void RaiseSummary(FanOut fan)
    {
        if (fan.Failures > 0)
        {
            alerts.Raise($"ノート「{NoteSendContent.Summary(fan.Change.Title)}」の通知を {fan.Failures} 人に送れませんでした。送り直します。({fan.FirstError})");
        }
    }

    /// <summary>時間が来た送り直しを、時間の目安の範囲で送る</summary>
    /// <param name="watch">この実行を始めてからの時間</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送ろうとしたら true</returns>
    private async Task<bool> SendDueRetriesAsync(Stopwatch watch, CancellationToken cancellationToken)
    {
        var any = false;
        var finals = new Dictionary<int, (string Title, int Count, string FirstError)>();
        while (watch.Elapsed < RunBudget)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<Retry> group;
            var nowUtc = time.GetUtcNow();
            lock (_retryGate)
            {
                group = [.. _retries.Where(retry => retry.DueAt <= nowUtc).Take(MaxParallelSends)];
                foreach (var retry in group)
                {
                    _retries.Remove(retry);
                }
            }

            if (group.Count == 0)
            {
                break;
            }

            any = true;
            var errors = await Task.WhenAll(group.Select(retry => SendAsync(retry, cancellationToken))).ConfigureAwait(false);
            for (var i = 0; i < group.Count; i++)
            {
                // 回数の上限での最後の失敗は、ノートごとにまとめて報告する
                if (errors[i].Length > 0 && group[i].Attempts + 1 >= SendRetryPolicy.MaxAttempts)
                {
                    var id = group[i].Change.Id;
                    finals[id] = finals.TryGetValue(id, out var current)
                        ? (current.Title, current.Count + 1, current.FirstError)
                        : (group[i].Change.Title, 1, errors[i]);
                }
            }
        }

        foreach (var final in finals.Values)
        {
            alerts.Raise($"ノート「{NoteSendContent.Summary(final.Title)}」の通知を {final.Count} 人に送れませんでした。送り直しの回数の上限に達しました。({final.FirstError})");
        }

        return any;
    }

    /// <summary>1 人へ 1 回送って、結果をログに残す。失敗は、回数の上限まで送り直しの予定にする</summary>
    /// <param name="retry">送る内容と相手。<see cref="Retry.Attempts"/> はこれまでに送ろうとした回数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>失敗の理由。成功は空文字</returns>
    private async Task<string> SendAsync(Retry retry, CancellationToken cancellationToken)
    {
        var attempt = retry.Attempts + 1;
        var channel = retry.Recipient.Channel;
        var error = "";
        if (channel.IsDeleted)
        {
            error = "送信先が削除されています。";
        }
        else if (!_notifiers.TryGetValue(channel.Kind, out var notifier))
        {
            error = "この種類の送信先には、まだ送れません。";
        }
        else
        {
            try
            {
                await notifier.SendAsync(
                    channel, NoteSendContent.FromChange(retry.Change.Id, retry.Change.Title, retry.EditorName, retry.IsCreated), cancellationToken).ConfigureAwait(false);
            }
            catch (SendFailedException ex)
            {
                error = ex.Message;
            }
        }

        await AppendLogAsync(retry, attempt, error, cancellationToken).ConfigureAwait(false);
        if (error.Length > 0 && attempt < SendRetryPolicy.MaxAttempts)
        {
            lock (_retryGate)
            {
                if (_retries.Count < MaxRetries)
                {
                    _retries.Add(retry with { Attempts = attempt, DueAt = time.GetUtcNow() + SendRetryPolicy.FailedWait(attempt) });
                }
            }
        }

        return error;
    }

    /// <summary>送信のログに 1 行足す</summary>
    /// <param name="retry">送った内容と相手</param>
    /// <param name="attempt">何回目の送信か</param>
    /// <param name="error">失敗の理由。成功は空文字</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>記録の完了を表すタスク</returns>
    /// <remarks>ログを書けなくても、送信そのものは失敗にしない。気づけるよう、通知は出す。</remarks>
    private async Task AppendLogAsync(Retry retry, int attempt, string error, CancellationToken cancellationToken)
    {
        var channel = retry.Recipient.Channel;
        try
        {
            await log.AppendAsync(
                new SendLogEntry(
                    time.GetLocalNow(), retry.Change.Id, retry.Change.Title, retry.Recipient.Name, channel.Id, channel.Kind, channel.Name,
                    attempt, error.Length == 0 ? SendStatus.Sent : SendStatus.Failed, error, NoteSendContent.Source, retry.EditorName),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DataFileException ex)
        {
            alerts.Raise(ex.Message);
        }
    }

    /// <summary>最後の失敗を記録する</summary>
    /// <param name="message">失敗のメッセージ。無ければ null</param>
    /// <returns>前回と違えば true</returns>
    private bool SetLastError(string? message)
    {
        var changed = LastError != message;
        LastError = message;
        return changed;
    }

    /// <summary>送る予定 1 件。ノートの更新ごと。通知する相手を、人の番号の順に、ページごとに読んで進める</summary>
    /// <param name="Change">更新されたノート</param>
    /// <param name="EditorName">更新した人の表示名</param>
    /// <param name="IsCreated">作成の通知か</param>
    private sealed record FanOut(NoteChange Change, string EditorName, bool IsCreated)
    {
        /// <summary>いま読んでいる相手のページ</summary>
        public IReadOnlyList<NoteRecipient> Page { get; set; } = [];

        /// <summary>ページの中で、次に送る位置</summary>
        public int Index { get; set; }

        /// <summary>次のページを読む起点。最後に読んだページの最後の人の番号</summary>
        public int LastUserId { get; set; }

        /// <summary>送れなかった人数</summary>
        public int Failures { get; set; }

        /// <summary>最初の失敗の理由</summary>
        public string FirstError { get; set; } = "";
    }

    /// <summary>送る内容と相手 1 人。送り直しの予定にも使う</summary>
    /// <param name="Change">更新されたノート</param>
    /// <param name="EditorName">更新した人の表示名</param>
    /// <param name="IsCreated">作成の通知か</param>
    /// <param name="Recipient">通知を受ける人と受け取り先</param>
    /// <param name="Attempts">これまでに送ろうとした回数</param>
    /// <param name="DueAt">送り直す時刻。最初の送信は使わない</param>
    private sealed record Retry(NoteChange Change, string EditorName, bool IsCreated, NoteRecipient Recipient, int Attempts, DateTimeOffset DueAt);
}
