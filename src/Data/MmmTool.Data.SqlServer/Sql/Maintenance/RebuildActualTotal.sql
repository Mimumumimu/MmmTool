-- 作業の実績の累計 (dbo.WorkItem.ActualTotalHours)を、記録 (dbo.WorkRecord)から作り直す。
-- 手で直した SQL などで、保存した値がずれたときに、管理者が実行する。普段は、記録を保存するたびに計算し直すので、使わない。
-- 何度実行しても結果は同じ。値が変わった作業だけ書き、更新日時も更新する (ほかの PC に、差分で届く)。DELETE 文は含まない。
-- 対象の DB を選んで、更新できる権限のあるユーザーで実行する。

UPDATE w
SET ActualTotalHours = t.Total,
    UpdatedAt = SYSDATETIMEOFFSET(),
    UpdatedByUserId = 0
FROM dbo.WorkItem AS w
CROSS APPLY (
    SELECT COALESCE(SUM(r.Hours), 0) AS Total
    FROM dbo.WorkRecord AS r
    WHERE r.WorkItemId = w.Id AND r.IsDeleted = 0
) AS t
WHERE w.IsDeleted = 0
  AND w.ActualTotalHours <> t.Total;
