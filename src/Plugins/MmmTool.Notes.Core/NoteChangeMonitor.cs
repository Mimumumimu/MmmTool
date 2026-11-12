using MmmSdk.Core.Components.Scheduling;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmTool.CodeMaster.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Notes.Core;

/// <summary>他の人が作成・更新したノートをアプリが動いている間、毎分 1 回探して、知らせる</summary>
/// <param name="notes">ノートの保存先</param>
/// <param name="settings">ユーザーごとの設定の保存先 (自分の「ノートの通知を受け取る」を読む)</param>
/// <param name="time">現在時刻の取得元</param>
/// <param name="ticker">定期処理の合図</param>
/// <param name="currentUser">今のユーザー (ログインの前かを読む前に確かめる)</param>
/// <remarks>
/// <para>
/// PC の通知ダイアログ用。
/// </para>
/// <para>
/// 通知の元はノートの更新日時。
/// 変更の履歴も、通知のための行も、DB に持たない。
/// 基準の時刻はメモリだけに持ち、ファイルにも DB にも書かない。
/// 最初に読んだときのデータベースサーバーの時刻が最初の基準で、止まっていた間 (アプリが動いていなかった間と、ログインの前)の更新は通知しない。
/// 基準は確定が遅れた書き込みを取りこぼさないよう <see cref="Overlap"/> 戻して読む。
/// 同じノートの同じ更新日時は 1 回しか知らせない。
/// 自分の編集は読んだ更新者の番号を見て、ここで外す。
/// 検索の条件には使わない。
/// </para>
/// <para>
/// ローカルモードと、ログインの前、接続できないときは読まずに次の周期へ進む。
/// 知らせる処理 (<c>notify</c>)はスレッドプールのスレッドから呼ぶ。
/// 画面を触るときは UI スレッドに切り替える。
/// </para>
/// </remarks>
public sealed class NoteChangeMonitor(INoteRepository notes, IAppUserSettingRepository settings, TimeProvider time, ITicker ticker, CurrentUser currentUser) : IDisposable
{
    /// <summary>確定が遅れた書き込みを取りこぼさないために基準から戻して読む時間</summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromSeconds(10);

    /// <summary>覚えた「知らせ済み」を捨てる古さ</summary>
    /// <remarks>戻して読む範囲より十分に長い</remarks>
    private static readonly TimeSpan SeenKeep = TimeSpan.FromMinutes(5);

    /// <summary>読む処理を止めるための取り消し</summary>
    /// <remarks>破棄で取り消す</remarks>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>毎分の合図の登録</summary>
    /// <remarks>始める前は null</remarks>
    private ITickSubscription? _subscription;

    /// <summary>知らせ済みのノート</summary>
    private readonly SeenChanges<int> _seen = new(SeenKeep);

    /// <summary>始めたか</summary>
    private bool _started;

    /// <summary>読む基準の時刻</summary>
    /// <remarks>
    /// 始めたときのデータベースサーバーの時刻から、読むたびに進む。
    /// 始めたときの時刻より前の更新は知らせない。
    /// </remarks>
    private readonly ChangeWatermark _watermark = new(Overlap);

    /// <summary>監視を始める</summary>
    /// <param name="isEnabled">
    /// ノートの機能がオンか。
    /// オフの間は読まず、知らせない。
    /// </param>
    /// <param name="notify">他の人の作成・更新を知らせる処理</param>
    /// <remarks>
    /// 機能をオンにするたびに起動時の準備が呼ばれる
    /// すでに始めているときは何もしない
    /// </remarks>
    public void Start(Func<bool> isEnabled, Action<IReadOnlyList<NoteNotice>> notify)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentNullException.ThrowIfNull(notify);
        if (_started)
        {
            return;
        }
        _started = true;

        // 毎分 1 回、端末ごとにずらした秒で読む。始めた直後にも 1 回読み、最初の基準を決める
        // 予想外の失敗 (壊れた行を読んだときなど)は、監視が黙って止まらないよう、合図の仕組みがアプリの受け皿に渡す
        var token = _stop.Token;
        _subscription = ticker.Subscribe(
            _ => CheckAsync(notify, token),
            new TickOptions { Timing = TickTiming.Staggered, RunImmediately = true, IsActive = isEnabled });
    }

    /// <summary>監視を止める</summary>
    public void Dispose()
    {
        _subscription?.Dispose();
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>読んで、他の人の作成・更新があれば知らせる</summary>
    /// <param name="notify">知らせる処理</param>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>
    /// 読む処理の結果を表すタスク。
    /// DB に届かなかったときは失敗で、続くと次の合図までの時間が延びる。
    /// 保存先が自分だけのとき、設定を読めないとき、ログインの前は読むものが無いので成功。
    /// </returns>
    private async Task<TickResult> CheckAsync(Action<IReadOnlyList<NoteNotice>> notify, CancellationToken cancellationToken)
    {
        // ローカルモード・設定を読めないとき・ログインの前は、読むものが無い。DB に届かなかったのではないので、失敗にしない。基準は決めず、次の周期でやり直す
        if (!notes.IsShared || !settings.IsAvailable || !currentUser.IsIdentified)
        {
            return TickResult.Done;
        }

        var (result, found) = await TryReadAsync(cancellationToken).ConfigureAwait(false);
        if (found is { Count: > 0 })
        {
            notify(found);
        }
        return result;
    }

    /// <summary>1 回読んで、知らせるものを決める</summary>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>
    /// 結果と、知らせるもの。
    /// DB に届かなかったときは失敗。
    /// </returns>
    private async Task<(TickResult Result, IReadOnlyList<NoteNotice>? Found)> TryReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

            // 最初の 1 回は、基準にするデータベースサーバーの時刻だけを受け取る。始める前の更新は、知らせない
            if (_watermark.Cursor is not { } cursor)
            {
                _watermark.Start((await notes.GetChangesAsync(null, today, cancellationToken).ConfigureAwait(false)).ServerNow);
                return (TickResult.Done, null);
            }

            var read = await notes.GetChangesAsync(_watermark.Since, today, cancellationToken).ConfigureAwait(false);
            var me = notes.CurrentUserId;
            var fresh = read.Changes
                .Where(change => _watermark.IsAfterFloor(change.UpdatedAt) && change.EditorUserId != me && !_seen.IsSeen(change.Id, change.UpdatedAt))
                .ToList();

            // DB を読む処理を、すべて終えてから、基準と「知らせ済み」を進める。途中で読めなかったときは進めず、次の周期で、同じ更新をもう一度読む
            var isOn = fresh.Count == 0 || await IsOnAsync(cancellationToken).ConfigureAwait(false);
            _watermark.Advance(read.ServerNow);
            foreach (var change in read.Changes)
            {
                _seen.Remember(change.Id, change.UpdatedAt);
            }
            _seen.Prune(_watermark.Cursor!.Value);

            return (TickResult.Done, fresh.Count == 0 || !isOn
                ? null
                : [.. fresh.Select(change => new NoteNotice(change, change.EditorName, IsCreated: change.CreatedAt > cursor))]);
        }
        catch (DataFileException)
        {
            // 接続できない。基準は進めず、次の周期でやり直す。失敗として伝え、続くと間隔を延ばす
            return (TickResult.Failed, null);
        }
    }

    /// <summary>自分の「ノートの通知を受け取る」がオンか</summary>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>
    /// オンなら true
    /// 行が無いときは既定のオフ
    /// </returns>
    private async Task<bool> IsOnAsync(CancellationToken cancellationToken)
    {
        var saved = await settings.GetAsync(CodeFeatures.Notes, NotificationFeatureSettings.CategoryNo, NotificationFeatureSettings.CodeNo, cancellationToken).ConfigureAwait(false);
        return NotificationFeatureSettings.IsOn(saved, defaultOn: false);
    }
}

/// <summary>他の人の作成・更新の知らせ 1 件</summary>
/// <param name="Change">更新されたノート</param>
/// <param name="EditorName">
/// 更新した人の表示名
/// 引けなかったときは空文字
/// </param>
/// <param name="IsCreated">
/// 作成の通知か
/// false は更新
/// 作成日時が前回の基準より後のとき true
/// </param>
public sealed record NoteNotice(NoteChange Change, string EditorName, bool IsCreated);
