using MmmSdk.Core.Components.Storage;

namespace MmmTool.Core.CliAssist;

/// <summary>定型コマンドの保存先</summary>
public interface ICliCommandRepository
{
    /// <summary>定型コマンドを読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>定型コマンドと、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無ければ既定の内容を作って保存する。壊れていたときは退避してから既定の内容で作り直す。</remarks>
    /// <exception cref="DataFileException">読み込み・保存に失敗した。</exception>
    Task<DataLoadResult<CliCommandSet>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>定型コマンドを上書き保存する</summary>
    /// <param name="commandSet">保存する定型コマンド</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>初期化 (既定の内容への作り直し)に使う。今の内容は引き継がず、退避もしない。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(CliCommandSet commandSet, CancellationToken cancellationToken = default);
}
