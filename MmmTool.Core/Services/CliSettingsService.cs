using MmmTool.Core.Entities;
using MmmTool.Core.Repositories;

namespace MmmTool.Core.Services;

/// <summary>
/// CLI補助の利用状態（作業ディレクトリ・送信履歴）をメモリに持ち、変更のたびに保存する。アプリ全体で 1 つ。
/// </summary>
public sealed class CliSettingsService(ICliSettingsRepository repository, TimeProvider timeProvider)
{
    public const int MaxSendHistory = 50;
    public const int MaxDirectoryHistory = 20;

    private CliSettings _settings = new();

    /// <summary>保存を止めているか。</summary>
    /// <remarks>読み込みに失敗したとき（手修正の誤り等）は、元のファイルを上書きで消さないよう保存を止める。</remarks>
    private bool _saveDisabled;

    /// <summary>読み込みに失敗したときのメッセージ。</summary>
    public string? LoadError { get; private set; }

    public string? LastDirectory => _settings.LastDirectory;

    public IReadOnlyList<string> DirectoryHistory => _settings.DirectoryHistory;

    public IReadOnlyList<SendHistoryEntry> SendHistory => _settings.SendHistory;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _settings = await repository.LoadAsync(cancellationToken);
            _settings.DirectoryHistory ??= [];
            _settings.SendHistory ??= [];
        }
        catch (DataFileException ex)
        {
            LoadError = $"{ex.Message}\n修正するまで、作業ディレクトリと送信履歴は保存されません。";
            _saveDisabled = true;
        }
    }

    /// <summary>送信した本文を履歴の先頭に追加する。空白のみは無視し、同じ本文は先頭へ移す。</summary>
    public Task AddSendHistoryAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        var history = _settings.SendHistory;
        history.RemoveAll(entry => entry.Text == text);
        history.Insert(0, new SendHistoryEntry { Text = text, SentAt = timeProvider.GetLocalNow() });
        TrimTo(history, MaxSendHistory);
        return SaveAsync(cancellationToken);
    }

    /// <summary>作業ディレクトリを移動したことを記録する（最終ディレクトリ・履歴の先頭）。</summary>
    public Task AddDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        _settings.LastDirectory = directory;

        var history = _settings.DirectoryHistory;
        history.RemoveAll(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, directory);
        TrimTo(history, MaxDirectoryHistory);
        return SaveAsync(cancellationToken);
    }

    public Task RemoveDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        _settings.DirectoryHistory.RemoveAll(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase));
        return SaveAsync(cancellationToken);
    }

    private Task SaveAsync(CancellationToken cancellationToken)
        => _saveDisabled ? Task.CompletedTask : repository.SaveAsync(_settings, cancellationToken);

    private static void TrimTo<T>(List<T> list, int maxCount)
    {
        if (list.Count > maxCount)
        {
            list.RemoveRange(maxCount, list.Count - maxCount);
        }
    }
}
