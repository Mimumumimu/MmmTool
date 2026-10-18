using System.Collections.Concurrent;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 探り (存在しない道への要求の繰り返し)を接続元ごとに数え、多すぎる接続元を一定時間断る。断った接続元の一覧も持つ。
/// </summary>
/// <remarks>
/// 数える接続元の数と、一覧の件数に上限がある。外から、メモリとファイルを際限なく増やされないようにするため。
/// </remarks>
internal sealed class ProbeGuard
{
    /// <summary>探りの回数の数え方の区切りの長さ。</summary>
    public static readonly TimeSpan ProbeWindow = TimeSpan.FromMinutes(1);

    /// <summary>探りが <see cref="ProbeLimit"/> に達した接続元を、断る長さ。</summary>
    public static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(30);

    /// <summary>区切りの中で、探りがこの回数に達した接続元を断る。</summary>
    public const int ProbeLimit = 10;

    /// <summary>数える接続元の上限。</summary>
    private const int MaxTracked = 100_000;

    /// <summary>断った接続元の一覧の上限。</summary>
    private const int MaxListed = 10_000;

    /// <summary>一覧に書く道の長さの上限。</summary>
    private const int MaxPathLength = 200;

    /// <summary>接続元ごとの、探りの数え方。</summary>
    private readonly ConcurrentDictionary<string, Tracking> _tracking = new();

    /// <summary>断った接続元の一覧。</summary>
    private readonly ConcurrentDictionary<string, Listed> _listed = new();

    /// <summary>一覧が変わったか。1 なら変わった。</summary>
    private int _changed;

    /// <summary>
    /// 道を、記録に書ける形にする。制御文字を置き換え、長いものは切る。
    /// </summary>
    /// <param name="path">要求の道。外から来る値。</param>
    /// <returns>記録に書ける形の道。</returns>
    public static string Sanitize(string path)
    {
        var text = path.Length > MaxPathLength ? path[..MaxPathLength] : path;
        return string.Concat(text.Select(c => char.IsControl(c) ? '?' : c));
    }

    /// <summary>
    /// 今、断っている接続元かを返す。
    /// </summary>
    /// <param name="address">接続元。</param>
    /// <param name="now">今の日時。</param>
    /// <returns>断っているなら true。</returns>
    public bool IsBlocked(string address, DateTimeOffset now)
    {
        if (!_tracking.TryGetValue(address, out var tracking))
        {
            return false;
        }
        lock (tracking)
        {
            return tracking.BlockedUntil > now;
        }
    }

    /// <summary>
    /// 探りを 1 回数える。回数が上限に達したら、その接続元を断り始める。
    /// </summary>
    /// <param name="address">接続元。</param>
    /// <param name="path">探った道。</param>
    /// <param name="now">今の日時。</param>
    /// <returns>この探りで、断り始めたなら true。</returns>
    public bool RecordProbe(string address, string path, DateTimeOffset now)
    {
        if (!_tracking.TryGetValue(address, out var tracking))
        {
            if (_tracking.Count >= MaxTracked)
            {
                return false;
            }
            tracking = _tracking.GetOrAdd(address, _ => new Tracking());
        }

        bool started;
        lock (tracking)
        {
            if (now - tracking.WindowStart >= ProbeWindow)
            {
                tracking.WindowStart = now;
                tracking.Count = 0;
            }
            tracking.Count++;
            started = tracking.Count >= ProbeLimit && tracking.BlockedUntil <= now;
            if (started)
            {
                tracking.BlockedUntil = now + BlockDuration;
            }
        }
        if (started)
        {
            GetOrAddListed(address, path, now);
        }
        return started;
    }

    /// <summary>
    /// 断った要求を 1 回、一覧に数える。
    /// </summary>
    /// <param name="address">接続元。</param>
    /// <param name="path">断った要求の道。</param>
    /// <param name="now">今の日時。</param>
    public void RecordRejection(string address, string path, DateTimeOffset now)
    {
        var listed = GetOrAddListed(address, path, now);
        if (listed is null)
        {
            return;
        }
        lock (listed)
        {
            listed.Count++;
            listed.LastAt = now;
            listed.LastPath = Sanitize(path);
        }
        MarkChanged();
    }

    /// <summary>
    /// 数え方のうち、もう要らないものを捨てる。
    /// </summary>
    /// <param name="now">今の日時。</param>
    public void Prune(DateTimeOffset now)
    {
        foreach (var (address, tracking) in _tracking)
        {
            bool idle;
            lock (tracking)
            {
                idle = tracking.BlockedUntil <= now && now - tracking.WindowStart >= ProbeWindow;
            }
            if (idle)
            {
                _tracking.TryRemove(address, out _);
            }
        }
    }

    /// <summary>
    /// 一覧を、最後に断った日時の新しい順で返す。
    /// </summary>
    /// <returns>一覧の写し。</returns>
    public IReadOnlyList<RejectedClientInfo> Snapshot()
    {
        var rows = new List<RejectedClientInfo>();
        foreach (var (address, listed) in _listed)
        {
            lock (listed)
            {
                rows.Add(new RejectedClientInfo(address, listed.FirstAt, listed.LastAt, listed.Count, listed.LastPath));
            }
        }
        return rows.OrderByDescending(row => row.LastAt).ToList();
    }

    /// <summary>
    /// 保存してあった一覧を戻す。
    /// </summary>
    /// <param name="rows">保存してあった一覧。</param>
    public void Restore(IEnumerable<RejectedClientInfo> rows)
    {
        foreach (var row in rows)
        {
            if (_listed.Count >= MaxListed)
            {
                return;
            }
            _listed[row.Address] = new Listed(row.FirstAt, row.LastPath) { LastAt = row.LastAt, Count = row.Count };
        }
    }

    /// <summary>
    /// 前回から一覧が変わったかを返し、変わっていない状態に戻す。
    /// </summary>
    /// <returns>変わっていたら true。</returns>
    public bool TakeChanged() => Interlocked.Exchange(ref _changed, 0) == 1;

    /// <summary>
    /// 一覧の項目を返す。無ければ、上限の範囲で作る。
    /// </summary>
    /// <param name="address">接続元。</param>
    /// <param name="path">要求の道。</param>
    /// <param name="now">今の日時。</param>
    /// <returns>項目。一覧が上限に達していて作れないときは null。</returns>
    private Listed? GetOrAddListed(string address, string path, DateTimeOffset now)
    {
        if (_listed.TryGetValue(address, out var existing))
        {
            return existing;
        }
        if (_listed.Count >= MaxListed)
        {
            return null;
        }
        var listed = _listed.GetOrAdd(address, _ => new Listed(now, Sanitize(path)));
        MarkChanged();
        return listed;
    }

    /// <summary>
    /// 一覧が変わったと覚える。
    /// </summary>
    private void MarkChanged() => Interlocked.Exchange(ref _changed, 1);

    /// <summary>
    /// 接続元ごとの、探りの数え方。
    /// </summary>
    private sealed class Tracking
    {
        /// <summary>今の区切りの始まりの日時。</summary>
        public DateTimeOffset WindowStart { get; set; }

        /// <summary>今の区切りの中の、探りの回数。</summary>
        public int Count { get; set; }

        /// <summary>断っている期限の日時。</summary>
        public DateTimeOffset BlockedUntil { get; set; }
    }

    /// <summary>
    /// 一覧の項目。
    /// </summary>
    /// <param name="firstAt">最初に断った日時。</param>
    /// <param name="lastPath">最後に要求した道。</param>
    private sealed class Listed(DateTimeOffset firstAt, string lastPath)
    {
        /// <summary>最初に断った日時。</summary>
        public DateTimeOffset FirstAt { get; } = firstAt;

        /// <summary>最後に断った日時。</summary>
        public DateTimeOffset LastAt { get; set; } = firstAt;

        /// <summary>断った回数。</summary>
        public int Count { get; set; }

        /// <summary>最後に要求した道。</summary>
        public string LastPath { get; set; } = lastPath;
    }
}
