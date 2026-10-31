using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Storage;
using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>
/// MmmTool と Web 版で書かれたメッセージの通知を、受ける人の受け取り先へ送る。1 秒おきの仕事。
/// </summary>
/// <remarks>
/// <para>
/// 1 回の実行 (<see cref="RunAsync"/>)は、送る対象 (送信待ち・送信中・失敗で上限に達していない通知の状態)ごとに、送る権利を取ってから送る。
/// 権利は、読んだ内容と同じときだけ更新できる条件つきの更新で取る。取れた (1 行更新できた)ものだけが送る。
/// 失敗 (<see cref="MessageSendStatus.Failed"/>)は、待つ時間を空けて、回数の上限まで送り直す (決まりは <see cref="SendRetryPolicy"/>)。
/// 送信中のまま長く止まっている行 (送っている途中で落ちた)も、送り直す。送り直しの前に送信が済んでいたときは、二重に送られうる (取りこぼすより、重複を許す側に倒す)。
/// </para>
/// <para>
/// リマインダーと違い、日付・時刻は見ない。書かれた順に、すぐ送る。1 秒おきの実行で拾うので、書いてから送るまで最大で約 1 秒かかる。
/// 失敗の理由は、DB には残さず、送信のログにだけ残す。
/// </para>
/// </remarks>
/// <param name="store">通知の状態の読み出しと保存先</param>
/// <param name="notifiers">外部へ送る口 (送信先の区分ごと)</param>
/// <param name="log">送信のログ (送った 1 回ごとの記録)</param>
/// <param name="alerts">気づいてほしい失敗の窓口</param>
/// <param name="time">現在時刻の提供元</param>
public sealed class MessageSendService(IMessageSendStore store, IEnumerable<INotifier> notifiers, ISendLog log, BatchAlertService alerts, TimeProvider time)
    : IBatchJob
{
    /// <summary>送るものがあるかを読む間隔</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>「最近の依頼」に出す最大の件数</summary>
    public const int RecentCount = 100;

    /// <summary>区分ごとの、外部へ送る口</summary>
    private readonly Dictionary<NotificationChannelKind, INotifier> _notifiers = notifiers.ToDictionary(notifier => notifier.Kind);

    /// <inheritdoc />
    public string Name => "メッセージの送信";

    /// <inheritdoc />
    /// <remarks>メッセージをすぐ送るため、1 秒おきに読む。書いてから送るまで最大でこの間隔で、読むのは状態の索引で絞った行だけで軽い。</remarks>
    public TimeSpan Interval => PollInterval;

    /// <inheritdoc />
    /// <remarks>任意のスレッドから発火する。</remarks>
    public event EventHandler? Changed;

    /// <inheritdoc />
    /// <remarks>任意のスレッドから発火する。</remarks>
    public event EventHandler? Ran;

    /// <inheritdoc />
    public string? LastError { get; private set; }

    /// <summary>送るものがあれば、送る</summary>
    /// <param name="now">今の日時 (ローカル。メッセージは日付・時刻を見ないので使わない)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    /// <remarks>
    /// DB の読み書きの失敗は、<see cref="LastError"/> に残して、その回を終える (次の分に、また試す)。送信の失敗は、行の状態に残す。
    /// 気づいてほしい失敗は、送信の失敗が最初の失敗と回数の上限での最後の失敗の 2 回だけ、DB の失敗が内容の変わったときだけ、窓口 (<see cref="BatchAlertService"/>)へ報告する。
    /// </remarks>
    public async Task RunAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var changed = false;
        try
        {
            // メッセージを無効にされた人への通知は、送る前に止める
            changed |= await store.SkipDisabledAsync(cancellationToken).ConfigureAwait(false) > 0;

            foreach (var target in await store.GetSendableAsync(SendRetryPolicy.MaxAttempts, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await ClaimAsync(target.Request, cancellationToken).ConfigureAwait(false) is not { } claimed)
                {
                    continue;
                }

                changed = true;
                await SendAsync(target, claimed, cancellationToken).ConfigureAwait(false);
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

    /// <summary>「最近の依頼」に出す依頼を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>依頼を新しい順に、最大 <see cref="RecentCount"/> 件。日付・状態では絞らない</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<MessageSendTarget>> GetRecentAsync(CancellationToken cancellationToken = default)
        => store.GetRecentAsync(RecentCount, cancellationToken);

    /// <summary>状態や待ち時間にかかわらず、今すぐ 1 回送る (画面の「再送」)</summary>
    /// <param name="target">送る対象</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信と記録の完了を表すタスク</returns>
    /// <remarks>
    /// 送信済み・回数の上限に達した失敗も送り直せる (回数は数え続ける)。
    /// 同じ送信を、別の MmmBatch・毎分の実行が同時に行っていたときは、先に権利を取ったほうだけが送り、こちらは何もしない。
    /// </remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった・保存できなかった (メッセージは画面に出せる)。</exception>
    public async Task ResendAsync(MessageSendTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!await store.TryClaimAsync(target.Request, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var claimed = target.Request with { Status = MessageSendStatus.Pending, Attempts = target.Request.Attempts + 1 };
        await SendAsync(target, claimed, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>送る権利を取る (済み・上限・待ち時間のものは取らない)</summary>
    /// <param name="current">今の依頼</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>権利を取れたときの、送信中の依頼。送らないとき (済み・上限・待ち時間・別の MmmBatch が先)は null</returns>
    private async Task<MessageDeliveryItem?> ClaimAsync(MessageDeliveryItem current, CancellationToken cancellationToken)
    {
        if (current.Status == MessageSendStatus.Sent || current.Attempts >= SendRetryPolicy.MaxAttempts)
        {
            return null;
        }

        // 足したばかりの依頼はすぐ。失敗は、回数 × 1 分、送信中のまま止まったものは 10 分、待ってから送り直す
        var wait = current.Status == MessageSendStatus.Failed
            ? SendRetryPolicy.FailedWait(current.Attempts)
            : current.Attempts == 0 ? TimeSpan.Zero : SendRetryPolicy.StalePendingAge;
        if (time.GetUtcNow() - current.UpdatedAt < wait)
        {
            return null;
        }

        return await store.TryClaimAsync(current, cancellationToken).ConfigureAwait(false)
            ? current with { Status = MessageSendStatus.Pending, Attempts = current.Attempts + 1 }
            : null;
    }

    /// <summary>送って、結果を行とログに残す</summary>
    /// <param name="target">送る対象</param>
    /// <param name="claimed">送る権利を取った、送信中の依頼</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信と記録の完了を表すタスク</returns>
    private async Task SendAsync(MessageSendTarget target, MessageDeliveryItem claimed, CancellationToken cancellationToken)
    {
        var error = "";
        if (target.Channel is null)
        {
            error = "受け取り先が指定されていません。";
        }
        else if (target.Channel.IsDeleted)
        {
            error = "送信先が削除されています。";
        }
        else if (!_notifiers.TryGetValue(target.Channel.Kind, out var notifier))
        {
            error = "この種類の送信先には、まだ送れません。";
        }
        else
        {
            try
            {
                await notifier.SendAsync(target.Channel, MessageSendContent.FromMessage(claimed.Body, claimed.MessageId, target.SenderName, target.GroupName), cancellationToken).ConfigureAwait(false);
            }
            catch (SendFailedException ex)
            {
                error = ex.Message;
            }
        }

        if (error.Length == 0)
        {
            await FinishAsync(target, claimed, SendStatus.Sent, "", cancellationToken).ConfigureAwait(false);
            return;
        }

        await FinishAsync(target, claimed, SendStatus.Failed, error, cancellationToken).ConfigureAwait(false);
        if (claimed.Attempts == 1 || claimed.Attempts >= SendRetryPolicy.MaxAttempts)
        {
            var place = target.Channel is null ? target.OwnerName : target.Channel.Name;
            alerts.Raise($"メッセージ「{MessageSendContent.Summary(claimed.Body)}」を「{place}」へ送れませんでした。({error})");
        }
    }

    /// <summary>結果を、依頼の行 (今の状態)に残し、送信のログに 1 行足す</summary>
    /// <param name="target">送った対象</param>
    /// <param name="claimed">送る権利を取った、送信中の依頼</param>
    /// <param name="status">結果 (<see cref="SendStatus.Sent"/> か <see cref="SendStatus.Failed"/>)</param>
    /// <param name="error">失敗の理由。成功は空文字。送信のログにだけ残す</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>記録の完了を表すタスク</returns>
    /// <remarks>ログを書けなくても、送信そのものは失敗にしない (気づけるよう、通知は出す)。</remarks>
    private async Task FinishAsync(MessageSendTarget target, MessageDeliveryItem claimed, SendStatus status, string error, CancellationToken cancellationToken)
    {
        if (status == SendStatus.Sent)
        {
            await store.MarkSentAsync(claimed.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await store.MarkFailedAsync(claimed.Id, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await log.AppendAsync(
                new SendLogEntry(
                    time.GetLocalNow(), claimed.Id, MessageSendContent.DisplayTitle(claimed.Body), target.OwnerName, target.Channel?.Id ?? 0,
                    target.Channel?.Kind ?? NotificationChannelKind.Ntfy, target.Channel?.Name ?? "",
                    claimed.Attempts, status, error, MessageSendContent.Source, target.SenderName),
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
}
