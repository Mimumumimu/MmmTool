namespace MmmTool.Core.Services;

/// <summary>添付ファイルの一時保存先（%TEMP%\MmmTool\session_日時\）の管理</summary>
/// <remarks>
/// アプリ全体で 1 つ。
/// <list type="bullet">
/// <item>最初の添付でセッションフォルダを作り、以降は連番を付けて保存する（同名でも衝突しない）</item>
/// <item>添付を全部取り除いたらフォルダごと削除して初期化する</item>
/// <item>送信済みのファイルは CLI が後から読むため、アプリ終了まで残す</item>
/// </list>
/// </remarks>
public sealed class AttachmentStore(TimeProvider timeProvider) : IDisposable
{
    /// <summary>古い一時フォルダとみなす経過時間。</summary>
    /// <remarks>前回までの残り（異常終了など）は、この時間より古いものだけ消す（同時起動している別ビルドのフォルダを消さないため）。</remarks>
    private static readonly TimeSpan StaleAge = TimeSpan.FromDays(1);

    /// <summary>一時フォルダのルート</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MmmTool");
    /// <summary>このインスタンスが作ったセッションフォルダ</summary>
    /// <remarks>終了時に削除する。</remarks>
    private readonly List<string> _ownedSessions = [];
    /// <summary>今のセッションフォルダ</summary>
    /// <remarks>未作成なら null。</remarks>
    private string? _session;
    /// <summary>セッション内の連番</summary>
    private int _sequence;
    /// <summary>古い一時フォルダの掃除を済ませたか</summary>
    private bool _staleCleaned;

    /// <summary>ファイルをコピーして添付する</summary>
    /// <remarks>保存先のパスを返す。</remarks>
    public async Task<string> AddFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var destination = NextPath(Path.GetFileName(sourcePath));
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await source.CopyToAsync(target, cancellationToken);
        return destination;
    }

    /// <summary>データをファイルとして添付する</summary>
    /// <remarks>保存先のパスを返す。</remarks>
    public async Task<string> AddAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var destination = NextPath(fileName);
        await File.WriteAllBytesAsync(destination, content, cancellationToken);
        return destination;
    }

    /// <summary>添付を取り除く</summary>
    /// <remarks>セッションに何も残らなければフォルダごと削除する。</remarks>
    public void Remove(string filePath)
    {
        File.Delete(filePath);

        if (_session is not null && !Directory.EnumerateFileSystemEntries(_session).Any())
        {
            Directory.Delete(_session);
            _ownedSessions.Remove(_session);
            _session = null;
        }
    }

    /// <summary>送信済みとして今のセッションを閉じる</summary>
    /// <remarks>ファイルは残し、次の添付は新しいセッションに入れる。</remarks>
    public void CloseSession() => _session = null;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var directory in _ownedSessions)
        {
            TryDeleteDirectory(directory);
        }
        _ownedSessions.Clear();
    }

    /// <summary>次に保存するファイルのパスを決める</summary>
    private string NextPath(string fileName)
    {
        if (_session is null)
        {
            StartSession();
        }
        _sequence++;
        return Path.Combine(_session!, $"{_sequence:D3}_{fileName}");
    }

    /// <summary>セッションフォルダを作る</summary>
    private void StartSession()
    {
        if (!_staleCleaned)
        {
            _staleCleaned = true;
            DeleteStaleSessions();
        }

        var now = timeProvider.GetLocalNow();
        var session = Path.Combine(_root, $"session_{now:yyyyMMdd_HHmmss_fff}");
        Directory.CreateDirectory(session);
        _ownedSessions.Add(session);
        _session = session;
        _sequence = 0;
    }

    /// <summary>古いセッションフォルダを削除する</summary>
    private void DeleteStaleSessions()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        var threshold = timeProvider.GetUtcNow().UtcDateTime - StaleAge;
        foreach (var directory in Directory.EnumerateDirectories(_root, "session_*"))
        {
            if (Directory.GetLastWriteTimeUtc(directory) < threshold)
            {
                TryDeleteDirectory(directory);
            }
        }
    }

    /// <summary>フォルダを削除する</summary>
    /// <remarks>失敗しても無視する。</remarks>
    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 後片付けなので、使用中などで消せなければ次回起動以降に回す
        }
    }
}
