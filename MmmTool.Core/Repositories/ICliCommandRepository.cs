using MmmSdk.Core.Repositories;
using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories;

/// <summary>定型コマンドの保存先</summary>
public interface ICliCommandRepository
{
    /// <summary>定型コマンドを読み込む</summary>
    /// <remarks>保存先にまだ無ければ既定の内容を作って保存する。壊れていたときは退避してから既定の内容で作り直す。</remarks>
    /// <exception cref="DataFileException">読み込み・保存に失敗した。</exception>
    Task<DataLoadResult<CliCommandSet>> LoadAsync(CancellationToken cancellationToken = default);
}
