-- 版 4: ノートの「全員」の枠が、作業報告の人の一覧を引くための索引を 3 つ足す。設計は docs/specs/work-report.md の「読み (保存先)」と docs/specs/database.md。
-- 何度流しても壊れない。無いものだけを作る。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。SQLite の 004_AddWorkReportPeopleIndexes.sql と同じ設計。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 予定 (状態が作業予定・作業中)と、期間内に完了した作業の担当者を、表を読まずに探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId ON dbo.WorkItem (StatusCodeNo, CompletedDate, AssigneeUserId) WHERE IsDeleted = 0;
GO

-- 期間内に記録がある作業の番号を、日付から探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkRecord_Date_WorkItemId' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE INDEX IX_WorkRecord_Date_WorkItemId ON dbo.WorkRecord (Date, WorkItemId) WHERE IsDeleted = 0;
GO

-- 報告日に日報メモがある人を、日付から探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkDayNote_Date_UserId' AND object_id = OBJECT_ID(N'dbo.WorkDayNote'))
CREATE INDEX IX_WorkDayNote_Date_UserId ON dbo.WorkDayNote (Date, UserId) WHERE IsDeleted = 0;
GO
