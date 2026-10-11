-- 作業の実績の累計 (WorkItem.ActualTotalHours)を、記録 (WorkRecord)から作り直す。
-- 手で直した SQL などで、保存した値がずれたときに、管理者が実行する。普段は、記録を保存するたびに計算し直すので、使わない。
-- 何度実行しても結果は同じ。値が変わった作業だけ書き、更新日時も更新する (ほかの PC に、差分で届く)。
-- データベースのファイルを、アプリを止めてから開いて流す。

UPDATE WorkItem
SET ActualTotalHours = (
        SELECT COALESCE(ROUND(SUM(r.Hours), 2), 0)
        FROM WorkRecord r
        WHERE r.WorkItemId = WorkItem.Id AND r.IsDeleted = 0
    ),
    UpdatedAt = strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00',
    UpdatedByUserId = 1
WHERE IsDeleted = 0
  AND ActualTotalHours <> (
        SELECT COALESCE(ROUND(SUM(r.Hours), 2), 0)
        FROM WorkRecord r
        WHERE r.WorkItemId = WorkItem.Id AND r.IsDeleted = 0
    );
