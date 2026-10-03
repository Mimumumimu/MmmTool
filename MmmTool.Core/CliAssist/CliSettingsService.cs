using MmmSdk.Core.Collections;
using MmmSdk.Core.Storage;

namespace MmmTool.Core.CliAssist;

/// <summary>
/// CLI補助の利用状態（作業ディレクトリ）をメモリに持ち、変更のたびに保存する。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">CLI補助の利用状態の保存先</param>
public sealed class CliSettingsService(ICliSettingsRepository repository)
{
    /// <summary>ディレクトリ履歴の最大件数</summary>
    public const int MaxDirectoryHistory = 20;

    /// <summary>現在の設定</summary>
    private CliSettings _settings = new();

    /// <summary>読み込みの結果</summary>
    /// <remarks>
    /// ファイルを読めなかったとき（ロック・権限など）は、元のファイルを上書きで消さないよう保存を止める（黙って保存しない。補助的な設定なので、例外にはしない）。
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
    /// 存在の確認は、UI スレッドを止めないよう、読み込み（<see cref="LoadAsync"/>）の中でバックグラウンドで行う（ネットワークパスで止まることがあるため）。
    /// </remarks>
    public string? StartDirectory { get; private set; }

    /// <summary>作業ディレクトリの履歴（新しい順）</summary>
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
            _settings.DirectoryHistory ??= [];
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

    /// <summary>作業ディレクトリを移動したことを記録する（最終ディレクトリ・履歴の先頭）。</summary>
    /// <param name="directory">移動した作業ディレクトリ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    public Task AddDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        _settings.LastDirectory = directory;

        var history = _settings.DirectoryHistory;
        history.AddRecent(directory, path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase), MaxDirectoryHistory);
        return SaveAsync(cancellationToken);
    }

    /// <summary>履歴から作業ディレクトリを取り除く</summary>
    /// <param name="directory">取り除く作業ディレクトリ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    public Task RemoveDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        _settings.DirectoryHistory.RemoveAll(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase));
        return SaveAsync(cancellationToken);
    }

    /// <summary>設定を保存する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存を止めているときは何もしない。</remarks>
    private Task SaveAsync(CancellationToken cancellationToken)
        => _status.HasFailed ? Task.CompletedTask : repository.SaveAsync(_settings, cancellationToken);
}
