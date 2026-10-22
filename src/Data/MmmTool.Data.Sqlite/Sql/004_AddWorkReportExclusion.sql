-- 版 4: 作業に「作業報告に含めない」列を足す。ノートの「全員」の枠が、作業報告の人の一覧を引くための索引を 3 つ足す。設計は docs/specs/work-report.md と docs/specs/database.md。
-- リリースまで、この版の変更はこのファイルに集める。SQL Server の 004_AddWorkReportExclusion.sql と同じ設計。
-- 列は版の番号で 1 回だけ流れる。SQLite の ADD COLUMN には、無いときだけ足す書き方が無いため。
-- 作業報告に含めない作業の印。定例の会議などに付け、1 は実績にも予定にも出さない。SQLite は列の位置を決められないので、表の末尾になる
ALTER TABLE WorkItem ADD COLUMN ReportExcluded INTEGER NOT NULL DEFAULT 0 CHECK (ReportExcluded IN (0, 1));

-- 予定 (状態が作業予定・作業中)と、期間内に完了した作業の担当者を、表を読まずに探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId ON WorkItem (StatusCodeNo, CompletedDate, AssigneeUserId) WHERE IsDeleted = 0;

-- 期間内に記録がある作業の番号を、日付から探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkRecord_Date_WorkItemId ON WorkRecord (Date, WorkItemId) WHERE IsDeleted = 0;

-- 報告日に日報メモがある人を、日付から探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkDayNote_Date_UserId ON WorkDayNote (Date, UserId) WHERE IsDeleted = 0;
