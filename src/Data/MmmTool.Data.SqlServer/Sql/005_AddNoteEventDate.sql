-- 版 5: ノートに「実施日」の列と、実施日を探す索引を足す。作業の列 ReportExcluded を、PlanExcluded に名前を変える。設計は docs/specs/notes.md と docs/specs/database.md。
-- 何度流しても壊れない。無いものだけを作る。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。SQLite は、同じ列と索引を、あるべき形の schema.sql が足す。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 作業報告の予定から外す印の列の名前。リリース済みの版 4 は、ReportExcluded の名前で足した。印は予定だけを外す (実績は外さない) ので、PlanExcluded にする。列があるときだけ変える。既定値の制約の名前もそろえる
IF COL_LENGTH(N'dbo.WorkItem', N'ReportExcluded') IS NOT NULL AND COL_LENGTH(N'dbo.WorkItem', N'PlanExcluded') IS NULL
EXEC sp_rename N'dbo.WorkItem.ReportExcluded', N'PlanExcluded', N'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_WorkItem_ReportExcluded' AND parent_object_id = OBJECT_ID(N'dbo.WorkItem'))
EXEC sp_rename N'dbo.DF_WorkItem_ReportExcluded', N'DF_WorkItem_PlanExcluded', N'OBJECT';
GO

-- 実施日。この日に、ノートのカードを目立たせ、起動時の通知ダイアログに出す。未設定は 9999-12-31。ALTER で付いた列は表の末尾にあるので、並びは docs/specs/database.md の Note の表のとおり EndDate の次に直す
IF COL_LENGTH(N'dbo.Note', N'EventDate') IS NULL
ALTER TABLE dbo.Note ADD EventDate date NOT NULL CONSTRAINT DF_Note_EventDate DEFAULT ('9999-12-31');
GO

-- 設定があるときは、開始日から終了日の間にある。列を足したのと同じバッチでは検査を足せないので、GO で分ける
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Note_EventDate' AND parent_object_id = OBJECT_ID(N'dbo.Note'))
ALTER TABLE dbo.Note ADD CONSTRAINT CK_Note_EventDate CHECK (EventDate = '9999-12-31' OR (EventDate >= StartDate AND EventDate <= EndDate));
GO

-- 実際の今日が実施日の行を探す。未設定の大量の行 (9999-12-31) を、起動のたびに走査しない。削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Note_EventDate' AND object_id = OBJECT_ID(N'dbo.Note'))
CREATE INDEX IX_Note_EventDate ON dbo.Note (EventDate);
GO
