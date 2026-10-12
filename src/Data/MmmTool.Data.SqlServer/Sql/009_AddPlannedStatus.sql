-- MmmTool の DB (SQL Server)。作業管理の状態に「作業予定」を足し、状態のコード番号を、表示順にそろえて振り直す。
-- 設計は docs/specs/work-report.md の「状態「作業予定」の追加」と「記録を作業ごと・日ごとに 1 件にする」。
-- やること: ① 状態のコード番号を 未対応 1 / 作業予定 2 / 作業中 3 / 完了 4 にする (コードマスタと、作業の StatusCodeNo)
--           ② 日ごとの記録の一意の索引を (WorkItemId, Date) に作り替える ③ 作業の担当者の索引を足す
-- 何度流しても壊れない (状態のコード番号 4 が無いときだけ、①を行う。索引は、無いときだけ作り、古い索引は、あるときだけ外す)。DELETE 文は含まない。
-- 古い作成スクリプト (004・005 など)は直さない。新しく作った DB は、005 で 1〜3 を入れたあと、この移行で 4 つにそろう。
-- 更新者は 0 (特定のユーザーではない)。更新日時を更新するので、各 PC に差分で届く。

-- 絞り込み付きの索引 (フィルター付き)を持つ表を更新するので、QUOTED_IDENTIFIER を ON にする (sqlcmd の既定は OFF。-I を付けても同じ)
SET QUOTED_IDENTIFIER ON;
GO

-- ① 状態のコード番号を振り直す。一意の索引に当たらないよう、完了 (3 → 4)、作業中 (2 → 3)、作業予定の追加 (2)の順に行う
IF NOT EXISTS (SELECT 1 FROM dbo.CodeMaster WHERE FeatureNo = 2 AND CategoryNo = 1 AND CodeNo = 4)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE dbo.CodeMaster
    SET CodeNo = 4, SortOrder = 4, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
    WHERE FeatureNo = 2 AND CategoryNo = 1 AND CodeNo = 3;

    UPDATE dbo.CodeMaster
    SET CodeNo = 3, SortOrder = 3, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
    WHERE FeatureNo = 2 AND CategoryNo = 1 AND CodeNo = 2;

    INSERT INTO dbo.CodeMaster (IsDeleted, CreatedByUserId, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
    VALUES (0, 0, 0, 2, 1, 2, 2, 0, N'作業予定');

    UPDATE dbo.WorkItem
    SET StatusCodeNo = CASE StatusCodeNo WHEN 3 THEN 4 WHEN 2 THEN 3 END, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
    WHERE StatusCodeNo IN (2, 3);

    COMMIT TRANSACTION;
END
GO

-- ② 日ごとの記録は、作業ごと・日ごとに 1 件 (人は区別しない。論理削除された行も含めて一意)。先に新しい索引を作り、成功してから古い索引を外す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkRecord_WorkItemId_Date' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
    CREATE UNIQUE INDEX UX_WorkRecord_WorkItemId_Date ON dbo.WorkRecord (WorkItemId, Date);
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkRecord_WorkItemId_Date_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
    DROP INDEX UX_WorkRecord_WorkItemId_Date_CreatedByUserId ON dbo.WorkRecord;
GO

-- ③ 作業報告の実績・予定 (担当が自分の作業を探す)。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_AssigneeUserId_StatusCodeNo' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
    CREATE INDEX IX_WorkItem_AssigneeUserId_StatusCodeNo ON dbo.WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;
GO
