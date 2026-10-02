using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories;

/// <summary>CLI補助の利用状態の保存先</summary>
public interface ICliSettingsRepository
{
    /// <summary>読み込む</summary>
    /// <remarks>保存先にまだ無ければ空の設定を返す。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<CliSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>利用状態を保存する</summary>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(CliSettings settings, CancellationToken cancellationToken = default);
}
