-- MmmTool の DB (SQL Server)。作業 (dbo.WorkItem)に、担当者の列 (AssigneeUserId)を足す。
-- 設計は docs/specs/work-items.md の「担当者」。何度流しても壊れない (列が無いときだけ足す)。DELETE 文・UPDATE 文は含まない。
-- AssigneeUserId は AppUser.Id。0 は未割り当て。外部キーは使わない。NULL は使わない。
-- 003 の CREATE TABLE は、列を画面の並び (Name → AssigneeUserId → PriorityCodeNo → 日付 → CompletedOn → 予定・実績 → 状態・進捗度 → Note)に置く。すでにある表には ALTER で末尾にしか足せないので、このスクリプトで足した表だけ、列の物理的な並びが違う (動作は同じ)。
-- これまでの作業は、既定の 0 (未割り当て)になる。担当は、画面から選んで入れる。
IF COL_LENGTH(N'dbo.WorkItem', N'AssigneeUserId') IS NULL
ALTER TABLE dbo.WorkItem ADD AssigneeUserId int NOT NULL
    CONSTRAINT DF_WorkItem_AssigneeUserId DEFAULT (0)
    CONSTRAINT CK_WorkItem_AssigneeUserId CHECK (AssigneeUserId >= 0);
GO
