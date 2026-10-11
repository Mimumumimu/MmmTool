using MmmSdk.Core.Components.Storage;

namespace MmmTool.WorkItems.Core;

/// <summary>作業管理の保存先</summary>
/// <remarks>
/// 利用者が選んだ案件の分だけを、必要なときに読む。書きは 1 件単位の操作で、更新は変えた列だけを書く。番号 (<c>Id</c>)は保存先が決める。
/// 削除は論理削除だけ (完全削除はしない)。失敗は <see cref="DataFileException"/>。
/// 入力の確認・並べ替えの計算・差分の反映は保存先ではなく <see cref="WorkItemService"/> が行い、保存先は行を読み書きするだけにする。
/// </remarks>
public interface IWorkItemRepository
{
    /// <summary>今のユーザーの番号 (日ごとの記録の持ち主)</summary>
    int UserId { get; }

    /// <summary>壊れたファイルを退避して作り直したときのメッセージ。通常は null</summary>
    string? RecoveryMessage { get; }

    /// <summary>担当者を扱う保存先か (ローカルモードは false。担当の列・絞り込みを出さない)</summary>
    bool HasAssignees { get; }

    /// <summary>担当者にできる人の一覧を読む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>全ユーザー (削除済みも含む。名前を引くため)。表示名の順</returns>
    /// <remarks>ローカルモードは、固定のローカルユーザー 1 人だけ。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default);

    /// <summary>表示する案件 (表示しない案件を除いた、全部の案件)の中身と、作業管理のコードマスタを読む</summary>
    /// <param name="hiddenProjectIds">表示しない案件の番号 (空なら、全部の案件を読む)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだ内容 (削除された案件は、含まれない)</returns>
    /// <remarks>表ごとに別の <c>SELECT</c>。表示しない案件の番号は 1 つの JSON で渡して、案件の番号で除くだけ。返すのは自分の表の列だけ。木はメモリで組み立てる。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<WorkLoadResult> LoadVisibleAsync(IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default);

    /// <summary>案件を番号で指定して、中身を読む</summary>
    /// <param name="projectIds">案件の番号 (少数。選びの変更や移動で、読み直す案件)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだ内容 (選んでいる案件の番号と、コードマスタは空)</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<WorkLoadResult> LoadProjectsAsync(IReadOnlyList<int> projectIds, CancellationToken cancellationToken = default);

    /// <summary>ある日の記録を読む</summary>
    /// <param name="date">日付</param>
    /// <param name="hiddenProjectIds">表示しない案件の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>表示する案件の、その日の記録 (全員分。論理削除済みを除く)</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkRecord>> LoadDayAsync(DateOnly date, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default);

    /// <summary>基準の時刻より後に更新された行を読む</summary>
    /// <param name="since">基準の時刻 (この時刻以後に更新された行を返す)</param>
    /// <param name="date">記録を読む日</param>
    /// <param name="hiddenProjectIds">表示しない案件の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新された行 (論理削除された行も含む。表示しない案件の行は含まない)</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<WorkChanges> GetChangesAsync(DateTimeOffset since, DateOnly date, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default);

    /// <summary>案件マスターを、名前で検索して読む</summary>
    /// <param name="query">検索語 (名前の一部。空なら絞らない)</param>
    /// <param name="activeOnly">完了済みの案件を除くか</param>
    /// <param name="hiddenProjectIds">表示しない案件の番号 (一覧の「表示しているか」の印と、先頭にまとめる並びに使う)</param>
    /// <param name="selectedFirst">表示している案件を、先頭にまとめるか</param>
    /// <param name="offset">読み始める位置</param>
    /// <param name="count">読む件数の上限</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>案件の一覧 (名前の順。選んでいる案件を先頭にまとめるときは、その中も名前の順。論理削除済みを除く)</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>案件を追加する</summary>
    /// <param name="name">名前</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>番号が決まった案件</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkProject> AddProjectAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>案件の名前・備考を更新する</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="name">新しい名前 (変えないなら null)</param>
    /// <param name="note">新しい備考 (変えないなら null)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>変えた列だけ書く。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdateProjectAsync(int projectId, string? name, string? note, CancellationToken cancellationToken = default);

    /// <summary>案件を論理削除する (案件の行だけ。中のグループ・作業の行は、そのまま残す)</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>グループを追加する</summary>
    /// <param name="group">追加するグループ (<see cref="WorkGroup.Id"/> は無視する)</param>
    /// <param name="siblingPositions">同時に直す、兄弟の位置 (無ければ空)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>番号が決まったグループ</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkGroup> AddGroupAsync(WorkGroup group, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default);

    /// <summary>作業を追加する</summary>
    /// <param name="item">追加する作業 (<see cref="WorkItem.Id"/> は無視する)</param>
    /// <param name="siblingPositions">同時に直す、兄弟の位置 (無ければ空)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>番号が決まった作業</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkItem> AddItemAsync(WorkItem item, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default);

    /// <summary>グループを更新する</summary>
    /// <param name="before">更新前の行</param>
    /// <param name="after">更新後の行</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>変えた列 (名前・備考)だけ書く。位置は <see cref="UpdatePositionsAsync"/> で変える。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdateGroupAsync(WorkGroup before, WorkGroup after, CancellationToken cancellationToken = default);

    /// <summary>作業を更新する</summary>
    /// <param name="before">更新前の行</param>
    /// <param name="after">更新後の行</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>
    /// 変えた列だけ書く (画面が持っている古い値で、他の人が書いた列を消さないため)。実績の累計・位置は書かない。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdateItemAsync(WorkItem before, WorkItem after, CancellationToken cancellationToken = default);

    /// <summary>行の位置 (案件・親・並び)を、まとめて更新する</summary>
    /// <param name="positions">更新する位置</param>
    /// <param name="touchedProjectIds">更新日時を更新する案件 (案件をまたぐ移動の、移動元と移動先。ほかの PC に、行が減ったことを差分で伝えるため。無ければ空)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>案件が変わる行は、その作業の記録の案件の番号も、同じトランザクションで直す。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdatePositionsAsync(IReadOnlyList<WorkPosition> positions, IReadOnlyList<int> touchedProjectIds, CancellationToken cancellationToken = default);

    /// <summary>グループと作業を論理削除する (渡された行だけ。グループの中の行は、そのまま残す)</summary>
    /// <param name="groupIds">削除するグループの番号</param>
    /// <param name="itemIds">削除する作業の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task DeleteAsync(IReadOnlyList<int> groupIds, IReadOnlyList<int> itemIds, CancellationToken cancellationToken = default);

    /// <summary>日ごとの記録を保存し、その作業の実績の累計を計算し直す</summary>
    /// <param name="record">保存する記録 (自分の分)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した記録と、計算し直した累計</returns>
    /// <remarks>
    /// 同じ作業・日付・人の行があれば使い回す。実績も備考も空 (<see cref="WorkRecord.IsEmpty"/>)なら、論理削除する。
    /// 同じトランザクションで、作業の行をロックして、記録をすべて足し直す (<see cref="WorkActualTotal"/>)。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkRecordResult> SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default);
}
