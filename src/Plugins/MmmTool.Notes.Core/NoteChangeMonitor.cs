using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmTool.CodeMaster.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Notes.Core;

/// <summary>
/// 他の人が作成・更新したノートを、アプリが動いている間、一定の間隔で探して、知らせる。PC の通知ダイアログ用。
/// </summary>
/// <param name="notes">ノートの保存先</param>
/// <param name="users">ユーザーの保存先 (更新した人の名前を引く)</param>
/// <param name="settings">ユーザーごとの設定の保存先 (自分の「ノートの通知を受け取る」を読む)</param>
/// <param name="time">現在時刻の取得元</param>
/// <remarks>
/// <para>
/// 通知の元は、ノートの更新日時。変更の履歴も、通知のための行も、DB に持たない。基準の時刻はメモリだけに持ち、ファイルにも DB にも書かない。
/// 始めたときのデータベースサーバーの時刻が最初の基準で、止まっていた間 (アプリが動いていなかった間)の更新は、通知しない。
/// 基準は、確定が遅れた書き込みを取りこぼさないよう <see cref="Overlap"/> 戻して読む。同じノートの同じ更新日時は、1 回しか知らせない。
/// 自分の編集は、読んだ更新者の番号を見て、ここで外す。検索の条件には使わない。
/// </para>
/// <para>
/// ローカルモードと、ログインの前、接続できないときは、読まずに次の周期へ進む。
/// 知らせる処理 (<c>notify</c>)は、スレッドプールのスレッドから呼ぶ。画面を触るときは、UI スレッドに切り替える。
/// </para>
/// </remarks>
public sealed class NoteChangeMonitor(INoteRepository notes, IAppUserRepository users, IAppUserSettingRepository settings, TimeProvider time) : IDisposable
{
    /// <summary>読む間隔</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <summary>確定が遅れた書き込みを取りこぼさないために、基準から戻して読む時間</summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromSeconds(10);

    /// <summary>覚えた「知らせ済み」を捨てる古さ。戻して読む範囲より十分に長い</summary>
    private static readonly TimeSpan SeenKeep = TimeSpan.FromMinutes(5);

    /// <summary>監視を止めるためのトークンの元</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>知らせ済みのノート (ノートの番号 → 知らせた更新日時)</summary>
    private readonly Dictionary<int, DateTimeOffset> _seen = [];

    /// <summary>始めたか</summary>
    private bool _started;

    /// <summary>次に読む基準。始めたときのデータベースサーバーの時刻から、読むたびに進む。始める前は null</summary>
    private DateTimeOffset? _cursor;

    /// <summary>始めたときのデータベースサーバーの時刻。これ以前の更新は、知らせない</summary>
    private DateTimeOffset _floor;

    /// <summary>監視を始める</summary>
    /// <param name="isEnabled">ノートの機能がオンか。オフの間は、読まず、知らせない</param>
    /// <param name="notify">他の人の作成・更新を知らせる処理</param>
    /// <remarks>機能をオンにするたびに、起動時の準備が呼ばれる。すでに始めているときは、何もしない。</remarks>
    public void Start(Func<bool> isEnabled, Action<IReadOnlyList<NoteNotice>> notify)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentNullException.ThrowIfNull(notify);
        if (_started)
        {
            return;
        }
        _started = true;

        // 予想外の失敗 (壊れた行を読んだときなど)は、監視が黙って止まらないよう、アプリの受け皿に渡す
        Task.Run(() => RunAsync(isEnabled, notify, _stop.Token)).Forget();
    }

    /// <summary>監視を止める</summary>
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>周期ごとに読んで、他の人の作成・更新があれば知らせる</summary>
    /// <param name="isEnabled">ノートの機能がオンか</param>
    /// <param name="notify">知らせる処理</param>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>監視を止めるまでの完了を表すタスク</returns>
    private async Task RunAsync(Func<bool> isEnabled, Action<IReadOnlyList<NoteNotice>> notify, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                if (isEnabled() && notes.IsShared && settings.IsAvailable
                    && await TryReadAsync(cancellationToken).ConfigureAwait(false) is { Count: > 0 } found)
                {
                    notify(found);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // 監視を止めた
        }
    }

    /// <summary>1 回読んで、知らせるものを決める</summary>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>知らせるもの。読めなかったとき (ログインの前・接続できない)は null</returns>
    private async Task<IReadOnlyList<NoteNotice>?> TryReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

            // 最初の 1 回は、基準にするデータベースサーバーの時刻だけを受け取る。始める前の更新は、知らせない
            if (_cursor is not { } cursor)
            {
                _floor = (await notes.GetChangesAsync(null, today, cancellationToken).ConfigureAwait(false)).ServerNow;
                _cursor = _floor;
                return null;
            }

            var read = await notes.GetChangesAsync(cursor - Overlap, today, cancellationToken).ConfigureAwait(false);
            _cursor = read.ServerNow > cursor ? read.ServerNow : cursor;

            var me = notes.CurrentUserId;
            var fresh = read.Changes
                .Where(change => change.UpdatedAt > _floor && change.EditorUserId != me && !IsSeen(change))
                .ToList();
            foreach (var change in read.Changes)
            {
                _seen[change.Id] = change.UpdatedAt;
            }
            Prune(_cursor.Value);

            if (fresh.Count == 0 || !await IsOnAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var names = (await users.GetUsersByIdsAsync([.. fresh.Select(change => change.EditorUserId).Distinct()], cancellationToken).ConfigureAwait(false))
                .ToDictionary(user => user.Id, user => user.DisplayName);
            return [.. fresh.Select(change => new NoteNotice(
                change,
                names.GetValueOrDefault(change.EditorUserId, ""),
                IsCreated: change.CreatedAt > cursor))];
        }
        catch (Exception ex) when (ex is DataFileException or InvalidOperationException)
        {
            // ログインの前・接続できない。基準は進めず、次の周期でやり直す
            return null;
        }
    }

    /// <summary>この更新を、すでに知らせた (読んだ)か</summary>
    /// <param name="change">更新されたノート</param>
    /// <returns>同じノートの、同じかそれより新しい更新日時を読み済みなら true</returns>
    private bool IsSeen(NoteChange change) => _seen.TryGetValue(change.Id, out var seen) && seen >= change.UpdatedAt;

    /// <summary>基準より十分に古い「知らせ済み」を捨てる (メモリを増やし続けないため)</summary>
    /// <param name="cursor">今の基準</param>
    private void Prune(DateTimeOffset cursor)
    {
        foreach (var id in _seen.Where(pair => pair.Value < cursor - SeenKeep).Select(pair => pair.Key).ToList())
        {
            _seen.Remove(id);
        }
    }

    /// <summary>自分の「ノートの通知を受け取る」がオンか</summary>
    /// <param name="cancellationToken">監視を止めるトークン</param>
    /// <returns>オンなら true。行が無いときは、既定のオフ</returns>
    private async Task<bool> IsOnAsync(CancellationToken cancellationToken)
    {
        var saved = await settings.GetAsync(CodeFeatures.Notes, NotificationFeatureSettings.CategoryNo, NotificationFeatureSettings.CodeNo, cancellationToken).ConfigureAwait(false);
        return NotificationFeatureSettings.IsOn(saved, defaultOn: false);
    }
}

/// <summary>
/// 他の人の作成・更新の知らせ 1 件。
/// </summary>
/// <param name="Change">更新されたノート</param>
/// <param name="EditorName">更新した人の表示名。引けなかったときは空文字</param>
/// <param name="IsCreated">作成の通知か。false は更新。作成日時が前回の基準より後のとき true</param>
public sealed record NoteNotice(NoteChange Change, string EditorName, bool IsCreated);
