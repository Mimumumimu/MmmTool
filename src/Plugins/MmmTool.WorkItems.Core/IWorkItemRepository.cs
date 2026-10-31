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
    /// <summary>今のユーザーの番号 (「自分の担当」の判定と、作成者・更新者に使う。日ごとの記録の持ち主ではない)</summary>
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
    /// <param name="projectIds">案件の番号</param>
    /// <param name="exclude">その番号を除くか (表示しない案件を除いて、表示する案件の全部を読むとき true。番号を指定して読むときは false)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>その日の記録 (全員分。論理削除済みを除く)</returns>
    /// <remarks>案件の番号で絞る (<c>(ProjectId, Date)</c> のインデックス)。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkRecord>> LoadDayAsync(DateOnly date, IReadOnlyCollection<int> projectIds, bool exclude, CancellationToken cancellationToken = default);

    /// <summary>案件の、すべての日の記録を読む (スケジュールの実績)</summary>
    /// <param name="projectIds">案件の番号</param>
    /// <param name="exclude">その番号を除くか (表示しない案件を除いて、表示する案件の全部を読むとき true。番号を指定して読むときは false)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実績が 0 より大きい記録 (全員分。論理削除済みと、備考だけの記録を除く)</returns>
    /// <remarks>日付で絞らない。案件の番号で絞る (<c>(ProjectId, Date)</c> のインデックス)。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkRecord>> LoadRecordsAsync(IReadOnlyCollection<int> projectIds, bool exclude, CancellationToken cancellationToken = default);

    /// <summary>自分の、ある日の日報メモを読む</summary>
    /// <param name="date">日付</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>日報メモ。その日の行が無ければ null (空のメモの行は、空のメモとして返す)</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<WorkDayNote?> LoadDayNoteAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>作業報告のために、担当が指定の人の作業と、その記録と、報告日のその人の日報メモを読む</summary>
    /// <param name="from">記録を読み始める日 (実績の開始日)</param>
    /// <param name="to">記録を読み終える日で、報告日。その日も含む</param>
    /// <param name="userId">人の番号。null は自分</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>担当が指定の人で、状態が作業予定・作業中か、期間内に記録があるか、期間内に完了した作業と、その作業が属する案件・グループ、期間内の記録、報告日のその人の日報メモ</returns>
    /// <remarks>
    /// 表示しない案件も含む。表示しないのは、この PC の見え方の設定のため。人で絞るのは作業の担当者 <see cref="WorkItem.AssigneeUserId"/> だけで、記録を付けた人は問わない。日報メモは、持ち主 <c>UserId</c> が指定の人の行だけ。
    /// 表ごとに別の <c>SELECT</c> で、1 回の往復で受け取る。
    /// </remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<WorkReportData> LoadReportAsync(DateOnly from, DateOnly to, int? userId = null, CancellationToken cancellationToken = default);

    /// <summary>作業報告のために、指定した全員分の作業と記録と日報メモを、1 回の往復で読み、人ごとに分けて返す</summary>
    /// <param name="from">記録を読み始める日 (実績の開始日)</param>
    /// <param name="to">記録を読み終える日で、報告日。その日も含む</param>
    /// <param name="userIds">人の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>人の番号ごとの、<see cref="LoadReportAsync"/> と同じ中身。作業も日報メモも無い人は、空の中身</returns>
    /// <remarks>人の数だけ <see cref="LoadReportAsync"/> を呼ぶ代わりに使う。分けるのは <see cref="WorkReportSplitter"/>。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyDictionary<int, WorkReportData>> LoadReportsAsync(DateOnly from, DateOnly to, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>作業報告に、実績か予定か日報メモがある人の番号を読む</summary>
    /// <param name="from">記録を読み始める日 (実績の開始日)</param>
    /// <param name="to">記録を読み終える日で、報告日。その日も含む</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>期間内に、実績・予定の作業がある担当者と、報告日に空でない日報メモがある人の番号</returns>
    /// <remarks>担当が決まっていない作業 (担当の番号が 0)は数えない。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<int>> GetReportPeopleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>実績のあった直近の日を探す (担当が自分の作業の工数が入った日か、作業を完了にした日の、新しいほう)</summary>
    /// <param name="before">この日より前から探す (報告日)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>工数が 0 より大きい記録の日と、完了日のうち、最新の日。無ければ null</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<DateOnly?> GetLatestRecordedDateAsync(DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>基準の時刻より後に更新された行を読む</summary>
    /// <param name="since">基準の時刻 (この時刻以後に更新された行を返す)</param>
    /// <param name="date">記録を読む日</param>
    /// <param name="allRecordDates">記録を、すべての日で読むか (スケジュールが記録を持っているとき true。false なら、<paramref name="date"/> の記録だけ)</param>
    /// <param name="hiddenProjectIds">表示しない案件の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新された行 (論理削除された行も含む。表示しない案件の行は含まない)</returns>
    /// <remarks>記録も、案件・グループ・作業と同じ 1 回の往復で受け取る (取り込みを、別の問い合わせに分けない)。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<WorkChanges> GetChangesAsync(DateTimeOffset since, DateOnly date, bool allRecordDates, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default);

    /// <summary>自分の、ある日の日報メモを保存する</summary>
    /// <param name="date">日付</param>
    /// <param name="note">メモ (空なら、空で上書きする)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した日報メモ</returns>
    /// <remarks>同じ (持ち主, 日付)の行があれば使い回し、無ければ作る。空にしても、行は消さず (論理削除しない)、メモを空にする。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkDayNote> SetDayNoteAsync(DateOnly date, string note, CancellationToken cancellationToken = default);

    /// <summary>案件マスターを、名前で検索して読む</summary>
    /// <param name="query">検索語 (名前の一部。空なら絞らない)</param>
    /// <param name="activeOnly">完了済みの案件を除くか</param>
    /// <param name="selectedOnly">表示している案件だけを読むか (表示しない案件を除く)</param>
    /// <param name="hiddenProjectIds">表示しない案件の番号 (一覧の「表示しているか」の印と、先頭にまとめる並びに使う)</param>
    /// <param name="selectedFirst">表示している案件を、先頭にまとめるか</param>
    /// <param name="offset">読み始める位置</param>
    /// <param name="count">読む件数の上限</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>案件の一覧 (名前の順。選んでいる案件を先頭にまとめるときは、その中も名前の順。論理削除済みを除く)</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>案件を追加する</summary>
    /// <param name="name">名前</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>番号が決まった案件</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkProject> AddProjectAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>案件の名前・リンク・備考を更新する</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="name">新しい名前 (変えないなら null)</param>
    /// <param name="link">新しいリンク (変えないなら null。空文字はリンクを消す)</param>
    /// <param name="note">新しい備考 (変えないなら null)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>変えた列だけ書く。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdateProjectAsync(int projectId, string? name, string? link, string? note, CancellationToken cancellationToken = default);

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
    /// <param name="record">保存する記録</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した記録と、計算し直した累計</returns>
    /// <remarks>
    /// 同じ作業・日付の行があれば使い回す (人は区別しない。ほかの人の記録も、同じ 1 件を直す)。実績も備考も空 (<see cref="WorkRecord.IsEmpty"/>)なら、論理削除する。
    /// 同じトランザクションで、作業の行をロックして、記録をすべて足し直す (<see cref="WorkActualTotal"/>)。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkRecordResult> SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default);

    /// <summary>自動入力の記録を、同じ作業・日付の行が無いときだけ入れ、その作業の実績の累計を計算し直す</summary>
    /// <param name="record">入れる記録</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>入れた記録と、計算し直した累計。行があって何も入れなかったときは null</returns>
    /// <remarks>
    /// 論理削除された行も「ある」と数えるので、0 にして消した日は戻さない。別の PC が先に入れた行も書き換えない。
    /// 作業の行のロックと累計の足し直しは <see cref="SetRecordAsync"/> と同じ。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkRecordResult?> AddRecordIfAbsentAsync(WorkRecord record, CancellationToken cancellationToken = default);

    /// <summary>自動入力の設定がある、自分の作業を読む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>担当が自分で、完了していなくて、自動入力の時間が 0 より大きい作業</returns>
    /// <remarks>表示しない案件の作業も含む。画面に読み込んだ作業には頼らない。削除された案件の作業は除く。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<IReadOnlyList<WorkItem>> LoadAutoEntryItemsAsync(CancellationToken cancellationToken = default);
}
