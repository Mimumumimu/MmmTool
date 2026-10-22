-- 版 4: ノートの「全員」の枠が、作業報告の人の一覧を引くための索引を 3 つ足す。設計は docs/specs/work-report.md の「読み (保存先)」と docs/specs/database.md。
-- 何度流しても壊れない。無いものだけを作る。リリースまで、この版の変更はこのファイルに集める。SQL Server の 004_AddWorkReportExclusion.sql と同じ設計。作業の列 ReportExcluded は、このスクリプトではなく、schema.sql が足す。

-- 予定 (状態が作業予定・作業中)と、期間内に完了した作業の担当者を、表を読まずに探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId ON WorkItem (StatusCodeNo, CompletedDate, AssigneeUserId) WHERE IsDeleted = 0;

-- 期間内に記録がある作業の番号を、日付から探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkRecord_Date_WorkItemId ON WorkRecord (Date, WorkItemId) WHERE IsDeleted = 0;

-- 報告日に日報メモがある人を、日付から探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkDayNote_Date_UserId ON WorkDayNote (Date, UserId) WHERE IsDeleted = 0;
