using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories;

public interface ICliCommandRepository
{
    /// <summary>定型コマンドを読み込む。保存先にまだ無ければ既定の内容を作って保存する。</summary>
    /// <exception cref="DataFileException">読み込み・保存に失敗した。</exception>
    Task<CliCommandSet> LoadAsync(CancellationToken cancellationToken = default);
}
