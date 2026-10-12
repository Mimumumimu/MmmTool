-- MmmTool の DB (SQL Server)。案件 (dbo.WorkProject)・グループ (dbo.WorkGroup)・作業 (dbo.WorkItem)に、リンクの列 (Link)を足す。
-- 設計は docs/specs/work-items.md の「リンク」。何度流しても壊れない (列が無いときだけ足す)。DELETE 文・UPDATE 文は含まない。
-- Link は、URL・フォルダ・ファイルのパス (1 行に 1 つ。2000 文字まで)。今ある行は、既定の空文字 (リンクなし)になる。外部キーは使わない。NULL は使わない。
-- 003 の CREATE TABLE は、列を設計した並び (案件・グループは Name → Link → Note、作業は ActualTotalHours → Link → StatusCodeNo)に置く。
-- すでにある表には ALTER で末尾にしか足せないので、このスクリプトで足した表は、列の物理的な並びが違う (動作は同じ。並びは SSMS で直す)。

IF COL_LENGTH(N'dbo.WorkProject', N'Link') IS NULL
ALTER TABLE dbo.WorkProject ADD Link nvarchar(2000) NOT NULL
    CONSTRAINT DF_WorkProject_Link DEFAULT (N'');
GO

IF COL_LENGTH(N'dbo.WorkGroup', N'Link') IS NULL
ALTER TABLE dbo.WorkGroup ADD Link nvarchar(2000) NOT NULL
    CONSTRAINT DF_WorkGroup_Link DEFAULT (N'');
GO

IF COL_LENGTH(N'dbo.WorkItem', N'Link') IS NULL
ALTER TABLE dbo.WorkItem ADD Link nvarchar(2000) NOT NULL
    CONSTRAINT DF_WorkItem_Link DEFAULT (N'');
GO
