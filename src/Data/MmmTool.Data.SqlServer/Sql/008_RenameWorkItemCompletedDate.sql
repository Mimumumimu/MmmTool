-- MmmTool の DB (SQL Server)。作業 (dbo.WorkItem)の列名 CompletedOn を、他の日付の列 (StartDate・DueDate)と語尾をそろえて、CompletedDate に直す。
-- 列・既定値の制約・インデックスの名前を変える。何度流しても壊れない (古い名前があるときだけ直す)。DELETE 文・UPDATE 文は含まない。
-- 003 の CREATE TABLE は、はじめから CompletedDate で作るので、新しく作った DB では、何もしない。
IF COL_LENGTH(N'dbo.WorkItem', N'CompletedOn') IS NOT NULL
    EXEC sp_rename N'dbo.WorkItem.CompletedOn', N'CompletedDate', N'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_WorkItem_CompletedOn' AND parent_object_id = OBJECT_ID(N'dbo.WorkItem'))
    EXEC sp_rename N'dbo.DF_WorkItem_CompletedOn', N'DF_WorkItem_CompletedDate', N'OBJECT';
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_ProjectId_CompletedOn' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
    EXEC sp_rename N'dbo.WorkItem.IX_WorkItem_ProjectId_CompletedOn', N'IX_WorkItem_ProjectId_CompletedDate', N'INDEX';
GO
