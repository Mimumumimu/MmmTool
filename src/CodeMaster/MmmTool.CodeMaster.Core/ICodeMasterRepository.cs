using MmmSdk.Core.Components.Storage;

namespace MmmTool.CodeMaster.Core;

/// <summary>コードマスタの名前を、画面から編集するための保存先</summary>
/// <remarks>
/// 編集できるのは、ローカルモードだけ。DB モード (サーバーモード)では、<see cref="CanEdit"/> が false の実装 (<see cref="UnavailableCodeMasterRepository"/>)が入り、画面は編集の入口を出さない。
/// 将来、管理者だけに編集を許すときは、<see cref="CanEdit"/> の判定を変える。
/// 読み込みは、各機能の保存先 (作業管理なら <c>IWorkItemRepository</c>)が行うので、ここは編集に使う分だけを持つ。
/// </remarks>
public interface ICodeMasterRepository
{
    /// <summary>画面から編集できるか</summary>
    /// <remarks>ローカルモードは true、DB モードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool CanEdit { get; }

    /// <summary>区分の選択肢を読み込む</summary>
    /// <param name="featureNo">機能番号 (<see cref="CodeFeatures"/>)</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>論理削除されていない件 (表示順、同じならコード番号の順)</returns>
    /// <exception cref="DataFileException">読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<CodeMasterEntry>> LoadOptionsAsync(int featureNo, int categoryNo, CancellationToken cancellationToken = default);

    /// <summary>区分の選択肢の名前を保存する</summary>
    /// <param name="featureNo">機能番号 (<see cref="CodeFeatures"/>)</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="namesByCodeNo">コード番号ごとの新しい名前 (名前が変わらない件を含んでよい)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>名前が変わる件だけ書き、更新日時を進める。名前以外の列 (数値・表示順・論理削除)は変えない。</remarks>
    /// <exception cref="ArgumentException">名前が <see cref="CodeMasterEntry.NameMaxLength"/> を超えて長い。</exception>
    /// <exception cref="DataFileException">保存できなかった。メッセージは画面に出せる。</exception>
    Task SaveNamesAsync(int featureNo, int categoryNo, IReadOnlyDictionary<int, string> namesByCodeNo, CancellationToken cancellationToken = default);
}
