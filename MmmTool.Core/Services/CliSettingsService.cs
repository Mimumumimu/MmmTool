using MmmSdk.Core.Repositories;
using MmmTool.Core.Entities;
using MmmTool.Core.Repositories;

namespace MmmTool.Core.Services;

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

    /// <summary>保存を止めているか。</summary>
    /// <remarks>
    /// ファイルを読めなかったとき（ロック・権限など）は、元のファイルを上書きで消さないよう保存を止める。
    /// 中身が壊れていたときは退避済みなので止めない。
    /// </remarks>
    private bool _saveDisabled;

    /// <summary>読み込みに失敗したときのメッセージ。</summary>
    public string? LoadError { get; private set; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。</summary>
    public string? RecoveryMessage { get; private set; }

    /// <summary>最後に移動した作業ディレクトリ</summary>
    public string? LastDirectory => _settings.LastDirectory;

    /// <summary>作業ディレクトリの履歴（新しい順）</summary>
    public IReadOnlyList<string> DirectoryHistory => _settings.DirectoryHistory;

    /// <summary>設定を読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            (_settings, RecoveryMessage) = await repository.LoadAsync(cancellationToken);
            _settings.DirectoryHistory ??= [];
        }
        catch (DataFileException ex)
        {
            LoadError = $"{ex.Message}\n修正するまで、作業ディレクトリは保存されません。";
            _saveDisabled = true;
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
        history.RemoveAll(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, directory);
        TrimTo(history, MaxDirectoryHistory);
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
        => _saveDisabled ? Task.CompletedTask : repository.SaveAsync(_settings, cancellationToken);

    /// <summary>リストを先頭から指定件数に切り詰める</summary>
    /// <typeparam name="T">要素の型</typeparam>
    /// <param name="list">切り詰めるリスト</param>
    /// <param name="maxCount">残す最大件数</param>
    private static void TrimTo<T>(List<T> list, int maxCount)
    {
        if (list.Count > maxCount)
        {
            list.RemoveRange(maxCount, list.Count - maxCount);
        }
    }
}
