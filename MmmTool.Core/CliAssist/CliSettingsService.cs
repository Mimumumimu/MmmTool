using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;

namespace MmmTool.Core.CliAssist;

/// <summary>
/// CLI補助の利用状態 (作業ディレクトリ)をメモリに持ち、変更のたびに保存する。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">CLI補助の利用状態の保存先</param>
public sealed class CliSettingsService(ICliSettingsRepository repository)
{
    /// <summary>ディレクトリ履歴の最大件数</summary>
    public const int MaxDirectoryHistory = 20;

    /// <summary>現在の設定</summary>
    /// <remarks>
    /// 書き換えず、変更のたびに新しい設定へ差し替える。保存の途中 (スレッドプールでの JSON への変換)に、UI スレッドで履歴を変えても、
    /// 保存中のものは変わらない (列挙中の変更による例外を防ぐ)。
    /// </remarks>
    private CliSettings _settings = new();

    /// <summary>保存の順序を守るロック (古い内容が後から書かれないように)</summary>
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>読み込みの結果</summary>
    /// <remarks>
    /// ファイルを読めなかったとき (ロック・権限など)は、元のファイルを上書きで消さないよう保存を止める (黙って保存しない。補助的な設定なので、例外にはしない)。
    /// 中身が壊れていたときは退避済みなので止めない。
    /// </remarks>
    private readonly LoadStatus _status = new();

    /// <summary>読み込みに失敗したときのメッセージ。</summary>
    public string? LoadError => _status.LoadError;

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。</summary>
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <summary>最後に移動した作業ディレクトリ</summary>
    public string? LastDirectory => _settings.LastDirectory;

    /// <summary>起動時にシェルを始める作業ディレクトリ</summary>
    /// <remarks>
    /// 最後に移動した作業ディレクトリが、読み込み時に存在していたときだけ。無い・調べていないときは null。
    /// 存在の確認は、UI スレッドを止めないよう、読み込み (<see cref="LoadAsync"/>)の中でバックグラウンドで行う (ネットワークパスで止まることがあるため)。
    /// </remarks>
    public string? StartDirectory { get; private set; }

    /// <summary>作業ディレクトリの履歴 (新しい順)</summary>
    public IReadOnlyList<string> DirectoryHistory => _settings.DirectoryHistory;

    /// <summary>設定を読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (settings, recoveryMessage) = await repository.LoadAsync(cancellationToken);
            _settings = settings;
            _status.Succeeded(recoveryMessage);
            if (_settings.LastDirectory is { } last && await Task.Run(() => Directory.Exists(last), cancellationToken).ConfigureAwait(false))
            {
                StartDirectory = last;
            }
        }
        catch (DataFileException ex)
        {
            _status.Failed(ex, $"{ex.Message}\n修正するまで、作業ディレクトリは保存されません。");
        }
    }

    /// <summary>作業ディレクトリを移動したことを記録する (最終ディレクトリ・履歴の先頭)。</summary>
    /// <param name="directory">移動した作業ディレクトリ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    public Task AddDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        var history = new List<string>(_settings.DirectoryHistory);
        history.AddRecent(directory, path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase), MaxDirectoryHistory);
        _settings = new CliSettings { LastDirectory = directory, DirectoryHistory = history };
        return SaveAsync(cancellationToken);
    }

    /// <summary>履歴から作業ディレクトリを取り除く</summary>
    /// <param name="directory">取り除く作業ディレクトリ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    public Task RemoveDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        _settings = new CliSettings
        {
            LastDirectory = _settings.LastDirectory,
            DirectoryHistory = [.. _settings.DirectoryHistory.Where(path => !string.Equals(path, directory, StringComparison.OrdinalIgnoreCase))],
        };
        return SaveAsync(cancellationToken);
    }

    /// <summary>設定を保存する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存を止めているときは何もしない。保存は順番に行い、そのときの最新の設定を書く。</remarks>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_status.HasFailed)
        {
            return;
        }

        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            await repository.SaveAsync(_settings, cancellationToken);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
