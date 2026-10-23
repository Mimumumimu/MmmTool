-- 版 5: ノートに「実施日」の列と、実施日を探す索引を足す。作業の列 ReportExcluded を、3 つの値を持てる ReportExclusion (tinyint)に変える。設計は docs/specs/notes.md・docs/specs/work-items.md・docs/specs/database.md。
-- 何度流しても壊れない。無いものだけを作る。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。SQLite は、同じ列と索引を、あるべき形の schema.sql が足す。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 作業が、作業報告のどの部分から外れるか。0 は外さない、1 は予定から外して実績には出す、2 は実績と予定の両方から外す。列の名前と型 (bit から tinyint)を、その場で変える。位置は変わらない
-- リリース済みの版 4 の列 ReportExcluded (bit)は、1 が実績と予定の両方から外す印だったので、新しい値の 2 にする
-- リリース前の版で名前を変えた列 PlanExcluded (bit)は、1 が予定だけから外す印だったので、新しい値の 1 のままにする
-- 列の名前を書く文は、列が無い DB でも検査で止まらないよう、EXEC の文字列にする。既定値の制約は、型を変える前に外す
IF COL_LENGTH(N'dbo.WorkItem', N'ReportExclusion') IS NULL AND COL_LENGTH(N'dbo.WorkItem', N'ReportExcluded') IS NOT NULL
BEGIN
    DECLARE @releasedDefault sysname = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'dbo.WorkItem') AND c.name = N'ReportExcluded');
    DECLARE @releasedDrop nvarchar(400) = N'ALTER TABLE dbo.WorkItem DROP CONSTRAINT ' + QUOTENAME(@releasedDefault);
    IF @releasedDefault IS NOT NULL EXEC (@releasedDrop);
    EXEC (N'ALTER TABLE dbo.WorkItem ALTER COLUMN ReportExcluded tinyint NOT NULL');
    EXEC (N'UPDATE dbo.WorkItem SET ReportExcluded = 2 WHERE ReportExcluded = 1');
    EXEC sp_rename N'dbo.WorkItem.ReportExcluded', N'ReportExclusion', N'COLUMN';
END
GO

IF COL_LENGTH(N'dbo.WorkItem', N'ReportExclusion') IS NULL AND COL_LENGTH(N'dbo.WorkItem', N'PlanExcluded') IS NOT NULL
BEGIN
    DECLARE @unreleasedDefault sysname = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'dbo.WorkItem') AND c.name = N'PlanExcluded');
    DECLARE @unreleasedDrop nvarchar(400) = N'ALTER TABLE dbo.WorkItem DROP CONSTRAINT ' + QUOTENAME(@unreleasedDefault);
    IF @unreleasedDefault IS NOT NULL EXEC (@unreleasedDrop);
    EXEC (N'ALTER TABLE dbo.WorkItem ALTER COLUMN PlanExcluded tinyint NOT NULL');
    EXEC sp_rename N'dbo.WorkItem.PlanExcluded', N'ReportExclusion', N'COLUMN';
END
GO

-- 既定値と、値の範囲の検査を足す。SQLite の schema.sql と同じ設計。列を変えたのと同じバッチでは書けないので、GO で分ける
IF COL_LENGTH(N'dbo.WorkItem', N'ReportExclusion') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_WorkItem_ReportExclusion' AND parent_object_id = OBJECT_ID(N'dbo.WorkItem'))
ALTER TABLE dbo.WorkItem ADD CONSTRAINT DF_WorkItem_ReportExclusion DEFAULT (0) FOR ReportExclusion;
GO

IF COL_LENGTH(N'dbo.WorkItem', N'ReportExclusion') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_WorkItem_ReportExclusion' AND parent_object_id = OBJECT_ID(N'dbo.WorkItem'))
ALTER TABLE dbo.WorkItem ADD CONSTRAINT CK_WorkItem_ReportExclusion CHECK (ReportExclusion IN (0, 1, 2));
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
