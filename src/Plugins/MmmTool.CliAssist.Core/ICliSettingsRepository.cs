using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core;

/// <summary>CLI補助の利用状態の保存先</summary>
public interface ICliSettingsRepository
{
    /// <summary>読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>利用状態と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無ければ空の設定を返す。壊れていたときは退避してから空の設定で作り直す。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<DataLoadResult<CliSettings>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>利用状態を保存する</summary>
    /// <param name="settings">保存する利用状態</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(CliSettings settings, CancellationToken cancellationToken = default);
}
