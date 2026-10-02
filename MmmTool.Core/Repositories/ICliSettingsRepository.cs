using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories;

public interface ICliSettingsRepository
{
    /// <summary>読み込む。保存先にまだ無ければ空の設定を返す。</summary>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<CliSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(CliSettings settings, CancellationToken cancellationToken = default);
}
