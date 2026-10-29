using System.Diagnostics;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Storage;
using MmmTool.Notes.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.NoteSending.Core;

/// <summary>
/// 他の人が作成・更新したノートを、「ノートの通知を受け取る」がオンの人の受け取り先へ送る。10 秒おきの仕事。
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
    /// <summary>更新されたノートを読む間隔。PC 版の通知ダイアログと同じ</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

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

    /// <summary>送る予定。ノートごとに 1 件で、先頭から 1 グループずつ送って、後ろに回す</summary>
    private readonly LinkedList<FanOut> _pending = new();

    /// <summary>送る予定の、ノートの番号ごとの引き当て。同じノートの更新は、新しい内容に置き換えるため</summary>
    private readonly Dictionary<int, LinkedListNode<FanOut>> _pendingByNote = [];

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
    /// <remarks>PC 版の通知ダイアログと同じ 10 秒おきに読む。ノートの通知は、書いたらすぐ届く必要がなく、読むのは、更新日時の索引で絞った行だけで軽い。</remarks>
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
                changed |= await SendPendingAsync(today, watch, cancellationToken).ConfigureAwait(false);
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
            var editorName = change.EditorName;
            _seen[change.Id] = change.UpdatedAt;
            var isCreated = change.CreatedAt > previous;
            if (_pendingByNote.TryGetValue(change.Id, out var node))
            {
                // 同じノートの予定が残っているときは、新しい内容に置き換えて、位置は動かさない。まだ送っていない人には、最新の内容だけを 1 回送る
                // 作成の通知がまだ届いていない人がいるので、作成の印は残す
                node.Value.Change = change;
                node.Value.EditorName = editorName;
                node.Value.IsCreated |= isCreated;
            }
            else
            {
                _pendingByNote[change.Id] = _pending.AddLast(new FanOut(change, editorName, isCreated));
            }
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

    /// <summary>送る予定を、先頭のノートから 1 グループずつ、時間の目安の範囲で送る</summary>
    /// <param name="today">今日。ノートがいまも通知の対象かを確かめるのに使う</param>
    /// <param name="watch">この実行を始めてからの時間</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送ろうとしたら true</returns>
    /// <remarks>
    /// 1 グループ送るたびに、そのノートを後ろに回す。人数の多いノートが、後ろのノートを待たせ続けないため。
    /// グループを送る前に、ノートがいまも通知の対象か (削除されていない・公開範囲が「全員」・終了日が今日以降)を、番号で確かめる。
    /// 送る予定に入れたあとで、非公開にされたノートのタイトルを、送ってしまわないため。外れていたら、その予定を捨てる。
    /// </remarks>
    /// <exception cref="DataFileException">通知する相手を読めなかった。送る予定は残るので、次の実行で続きから試す。</exception>
    private async Task<bool> SendPendingAsync(DateOnly today, Stopwatch watch, CancellationToken cancellationToken)
    {
        var any = false;
        while (_pending.First is { } node && watch.Elapsed < RunBudget)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fan = node.Value;
            if (!await source.IsNoticeTargetAsync(fan.Change.Id, today, cancellationToken).ConfigureAwait(false))
            {
                Remove(node);
                continue;
            }

            if (fan.Index >= fan.Page.Count)
            {
                var page = await source.GetRecipientsAsync(fan.LastUserId, fan.Change.EditorUserId, RecipientPageSize, cancellationToken).ConfigureAwait(false);
                if (page.Count == 0)
                {
                    Remove(node);
                    RaiseSummary(fan);
                    any = true;
                    continue;
                }

                fan.Page = page;
                fan.Index = 0;
                fan.LastUserId = page[^1].UserId;
            }

            // 同じノートが別の人に更新し直されたときは、新しい更新者に送らない。読み済みのページの中にいることがある
            var group = fan.Page.Skip(fan.Index).Take(MaxParallelSends).ToList();
            var results = await Task.WhenAll(group.Where(recipient => recipient.UserId != fan.Change.EditorUserId).Select(recipient => SendAsync(new Retry(fan.Change, fan.EditorName, fan.IsCreated, recipient, Attempts: 0, DateTimeOffset.MinValue), cancellationToken))).ConfigureAwait(false);
            fan.Index += group.Count;
            fan.Failures += results.Count(result => result.Error.Length > 0);
            fan.NotRetried += results.Count(result => result.Error.Length > 0 && !result.Retrying);
            fan.FirstError = fan.FirstError.Length > 0 ? fan.FirstError : results.FirstOrDefault(result => result.Error.Length > 0)?.Error ?? "";
            any = true;

            // 後ろに回す。ノートが 1 件だけのときは、位置は変わらない
            _pending.Remove(node);
            _pending.AddLast(node);
        }

        return any;
    }

    /// <summary>送る予定から、ノート 1 件を外す</summary>
    /// <param name="node">外す予定</param>
    private void Remove(LinkedListNode<FanOut> node)
    {
        _pending.Remove(node);
        _pendingByNote.Remove(node.Value.Change.Id);
    }

    /// <summary>ノート 1 件を送り終えたとき、送れなかった人がいれば、まとめて 1 回報告する</summary>
    /// <param name="fan">送り終えたノートの予定</param>
    private void RaiseSummary(FanOut fan)
    {
        if (fan.Failures == 0)
        {
            return;
        }

        // 送り直しの予定がいっぱいで入れられなかった人は、送り直せない。事実のとおりに知らせる
        var retryText = fan.NotRetried == 0 ? "送り直します。" : $"うち {fan.NotRetried} 人は、送り直しの予定がいっぱいで、送り直せません。";
        alerts.Raise($"ノート「{NoteSendContent.Summary(fan.Change.Title)}」の通知を {fan.Failures} 人に送れませんでした。{retryText}({fan.FirstError})");
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

            // 送り直す前に、その人が、いまも通知を受け取る人か確かめる。オフにした人・受け取り先を外した人には、送り直さない
            var targets = new List<Retry>();
            foreach (var retry in group)
            {
                if (await FindRecipientAsync(retry, cancellationToken).ConfigureAwait(false) is { } current)
                {
                    targets.Add(retry with { Recipient = current });
                }
            }

            var results = await Task.WhenAll(targets.Select(retry => SendAsync(retry, cancellationToken))).ConfigureAwait(false);
            for (var i = 0; i < targets.Count; i++)
            {
                // 回数の上限での最後の失敗は、ノートごとにまとめて報告する
                if (results[i].Error.Length > 0 && targets[i].Attempts + 1 >= SendRetryPolicy.MaxAttempts)
                {
                    var id = targets[i].Change.Id;
                    finals[id] = finals.TryGetValue(id, out var current)
                        ? (current.Title, current.Count + 1, current.FirstError)
                        : (targets[i].Change.Title, 1, results[i].Error);
                }
            }
        }

        foreach (var final in finals.Values)
        {
            alerts.Raise($"ノート「{NoteSendContent.Summary(final.Title)}」の通知を {final.Count} 人に送れませんでした。送り直しの回数の上限に達しました。({final.FirstError})");
        }

        return any;
    }

    /// <summary>送り直しの相手が、いまも通知を受け取る人か確かめる。送る先が変わっていたら、新しい送る先を返す</summary>
    /// <param name="retry">送り直しの予定</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>いまの相手と受け取り先。オフにした・受け取り先を外した・管理者が無効にした人は null。DB を読めなかったときは、前の相手のまま返す (送り直しを、一時的な失敗で捨てないため)</returns>
    private async Task<NoteRecipient?> FindRecipientAsync(Retry retry, CancellationToken cancellationToken)
    {
        try
        {
            var found = await source.GetRecipientsAsync(retry.Recipient.UserId - 1, 0, 1, cancellationToken).ConfigureAwait(false);
            return found.Count > 0 && found[0].UserId == retry.Recipient.UserId ? found[0] : null;
        }
        catch (DataFileException)
        {
            return retry.Recipient;
        }
    }

    /// <summary>1 人へ 1 回送って、結果をログに残す。失敗は、回数の上限まで送り直しの予定にする</summary>
    /// <param name="retry">送る内容と相手。<see cref="Retry.Attempts"/> はこれまでに送ろうとした回数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>結果。失敗の理由と、送り直しの予定に入れたか</returns>
    private async Task<SendResult> SendAsync(Retry retry, CancellationToken cancellationToken)
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
        var retrying = false;
        if (error.Length > 0 && attempt < SendRetryPolicy.MaxAttempts)
        {
            lock (_retryGate)
            {
                if (_retries.Count < MaxRetries)
                {
                    _retries.Add(retry with { Attempts = attempt, DueAt = time.GetUtcNow() + SendRetryPolicy.FailedWait(attempt) });
                    retrying = true;
                }
            }
        }

        return new SendResult(error, retrying);
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

    /// <summary>送る予定 1 件。ノートごとに 1 件で、通知する相手を、人の番号の順に、ページごとに読んで進める</summary>
    /// <param name="change">更新されたノート</param>
    /// <param name="editorName">更新した人の表示名</param>
    /// <param name="isCreated">作成の通知か</param>
    private sealed class FanOut(NoteChange change, string editorName, bool isCreated)
    {
        /// <summary>更新されたノート。同じノートがまた更新されたときは、新しい内容に置き換わる</summary>
        public NoteChange Change { get; set; } = change;

        /// <summary>更新した人の表示名</summary>
        public string EditorName { get; set; } = editorName;

        /// <summary>作成の通知か。作成の通知がまだ届いていない人がいるので、一度 true になったら変えない</summary>
        public bool IsCreated { get; set; } = isCreated;

        /// <summary>いま読んでいる相手のページ</summary>
        public IReadOnlyList<NoteRecipient> Page { get; set; } = [];

        /// <summary>ページの中で、次に送る位置</summary>
        public int Index { get; set; }

        /// <summary>次のページを読む起点。最後に読んだページの最後の人の番号</summary>
        public int LastUserId { get; set; }

        /// <summary>送れなかった人数</summary>
        public int Failures { get; set; }

        /// <summary>送れなかった人のうち、送り直しの予定がいっぱいで、送り直せない人数</summary>
        public int NotRetried { get; set; }

        /// <summary>最初の失敗の理由</summary>
        public string FirstError { get; set; } = "";
    }

    /// <summary>1 人へ 1 回送った結果</summary>
    /// <param name="Error">失敗の理由。成功は空文字</param>
    /// <param name="Retrying">失敗して、送り直しの予定に入れたか</param>
    private sealed record SendResult(string Error, bool Retrying);

    /// <summary>送る内容と相手 1 人。送り直しの予定にも使う</summary>
    /// <param name="Change">更新されたノート</param>
    /// <param name="EditorName">更新した人の表示名</param>
    /// <param name="IsCreated">作成の通知か</param>
    /// <param name="Recipient">通知を受ける人と受け取り先</param>
    /// <param name="Attempts">これまでに送ろうとした回数</param>
    /// <param name="DueAt">送り直す時刻。最初の送信は使わない</param>
    private sealed record Retry(NoteChange Change, string EditorName, bool IsCreated, NoteRecipient Recipient, int Attempts, DateTimeOffset DueAt);
}
