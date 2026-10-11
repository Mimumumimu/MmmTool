-- MmmTool の DB (SQL Server)。作業管理の 4 つの表を、dbo スキーマに作る。
-- 設計は docs/specs/work-items.md と docs/specs/database.md。何度流しても壊れない (無いものだけを作る)。DELETE 文は含まない。
-- 使い方: 001 のあとに、同じ DB を選んで、表を作れる権限のあるユーザーで、全体を実行する。sqlcmd で流すときは、フィルターつきのインデックスに QUOTED_IDENTIFIER が要るので、-I を付ける (SSMS は既定でオン)。
-- 型: 日時は datetimeoffset(3)、日付は date (ない日は 9999-12-31)、時間は decimal(小数 2 桁)。外部キーは使わない。NULL は使わない。

-- 案件マスター。全員で共有する案件の 1 件
IF OBJECT_ID(N'dbo.WorkProject', N'U') IS NULL
CREATE TABLE dbo.WorkProject
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkProject PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_WorkProject_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkProject_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_WorkProject_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkProject_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_WorkProject_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    Name            nvarchar(200)   NOT NULL CONSTRAINT CK_WorkProject_Name CHECK (LEN(Name) >= 1),
    Note            nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkProject_Note DEFAULT (N'')
);
GO

-- 差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkProject_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkProject'))
CREATE INDEX IX_WorkProject_UpdatedAt ON dbo.WorkProject (UpdatedAt);
GO

-- グループ。ParentGroupId の 0 は案件の直下。SortOrder は、グループと作業にまたがる、兄弟の中の並び
IF OBJECT_ID(N'dbo.WorkGroup', N'U') IS NULL
CREATE TABLE dbo.WorkGroup
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkGroup PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_WorkGroup_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkGroup_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_WorkGroup_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkGroup_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_WorkGroup_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ProjectId       int             NOT NULL,
    ParentGroupId   int             NOT NULL CONSTRAINT DF_WorkGroup_ParentGroupId DEFAULT (0) CONSTRAINT CK_WorkGroup_ParentGroupId CHECK (ParentGroupId >= 0),
    SortOrder       int             NOT NULL CONSTRAINT DF_WorkGroup_SortOrder DEFAULT (0) CONSTRAINT CK_WorkGroup_SortOrder CHECK (SortOrder >= 0),
    Name            nvarchar(200)   NOT NULL CONSTRAINT CK_WorkGroup_Name CHECK (LEN(Name) >= 1),
    Note            nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkGroup_Note DEFAULT (N'')
);
GO

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkGroup_ProjectId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkGroup'))
CREATE INDEX IX_WorkGroup_ProjectId_UpdatedAt ON dbo.WorkGroup (ProjectId, UpdatedAt);
GO

-- 作業。GroupId の 0 は案件の直下。日付のない日は、開始日が 0001-01-01、期限日が 9999-12-31、PlannedHours の 0 は予定なし、CompletedOn の 9999-12-31 は未完了
-- ActualTotalHours は、その作業の全員の記録の合計 (記録を保存するたびに計算し直して書く)
-- AssigneeUserId は担当者 (AppUser.Id。0 は未割り当て)
-- PriorityCodeNo・StatusCodeNo・ProgressCodeNo は、コードマスタ (004_CreateCodeMaster.sql の CodeMaster)のコード番号 (区分 3・1・2)。外部キーは使わない
IF OBJECT_ID(N'dbo.WorkItem', N'U') IS NULL
CREATE TABLE dbo.WorkItem
(
    Id               int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkItem PRIMARY KEY,
    IsDeleted        bit             NOT NULL CONSTRAINT DF_WorkItem_IsDeleted DEFAULT (0),
    CreatedAt        datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkItem_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId  int             NOT NULL CONSTRAINT CK_WorkItem_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt        datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkItem_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId  int             NOT NULL CONSTRAINT CK_WorkItem_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ProjectId        int             NOT NULL,
    GroupId          int             NOT NULL CONSTRAINT DF_WorkItem_GroupId DEFAULT (0) CONSTRAINT CK_WorkItem_GroupId CHECK (GroupId >= 0),
    SortOrder        int             NOT NULL CONSTRAINT DF_WorkItem_SortOrder DEFAULT (0) CONSTRAINT CK_WorkItem_SortOrder CHECK (SortOrder >= 0),
    Name             nvarchar(200)   NOT NULL CONSTRAINT CK_WorkItem_Name CHECK (LEN(Name) >= 1),
    AssigneeUserId   int             NOT NULL CONSTRAINT DF_WorkItem_AssigneeUserId DEFAULT (0) CONSTRAINT CK_WorkItem_AssigneeUserId CHECK (AssigneeUserId >= 0),
    PriorityCodeNo   int             NOT NULL CONSTRAINT DF_WorkItem_PriorityCodeNo DEFAULT (2) CONSTRAINT CK_WorkItem_PriorityCodeNo CHECK (PriorityCodeNo >= 1),
    StartDate        date            NOT NULL CONSTRAINT DF_WorkItem_StartDate DEFAULT ('0001-01-01'),
    DueDate          date            NOT NULL CONSTRAINT DF_WorkItem_DueDate DEFAULT ('9999-12-31'),
    PlannedHours     decimal(6, 2)   NOT NULL CONSTRAINT DF_WorkItem_PlannedHours DEFAULT (0) CONSTRAINT CK_WorkItem_PlannedHours CHECK (PlannedHours >= 0),
    ActualTotalHours decimal(12, 2)  NOT NULL CONSTRAINT DF_WorkItem_ActualTotalHours DEFAULT (0) CONSTRAINT CK_WorkItem_ActualTotalHours CHECK (ActualTotalHours >= 0),
    StatusCodeNo     int             NOT NULL CONSTRAINT DF_WorkItem_StatusCodeNo DEFAULT (1) CONSTRAINT CK_WorkItem_StatusCodeNo CHECK (StatusCodeNo >= 1),
    ProgressCodeNo   int             NOT NULL CONSTRAINT DF_WorkItem_ProgressCodeNo DEFAULT (1) CONSTRAINT CK_WorkItem_ProgressCodeNo CHECK (ProgressCodeNo >= 1),
    Note             nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkItem_Note DEFAULT (N''),
    CompletedOn      date            NOT NULL CONSTRAINT DF_WorkItem_CompletedOn DEFAULT ('9999-12-31')
);
GO

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_ProjectId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_ProjectId_UpdatedAt ON dbo.WorkItem (ProjectId, UpdatedAt);
GO

-- 案件の完了の計算 (削除されていない行だけ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_ProjectId_CompletedOn' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_ProjectId_CompletedOn ON dbo.WorkItem (ProjectId, CompletedOn) WHERE IsDeleted = 0;
GO

-- 日ごとの記録。CreatedByUserId が記録した人 (人ごとに 1 件)。ProjectId は作業の案件 (選んだ案件に絞って読むため)。Id は bigint
IF OBJECT_ID(N'dbo.WorkRecord', N'U') IS NULL
CREATE TABLE dbo.WorkRecord
(
    Id              bigint          IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkRecord PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_WorkRecord_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkRecord_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_WorkRecord_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkRecord_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_WorkRecord_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ProjectId       int             NOT NULL,
    WorkItemId      int             NOT NULL,
    Date            date            NOT NULL,
    Hours           decimal(4, 2)   NOT NULL CONSTRAINT DF_WorkRecord_Hours DEFAULT (0) CONSTRAINT CK_WorkRecord_Hours CHECK (Hours >= 0 AND Hours <= 24),
    Note            nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkRecord_Note DEFAULT (N'')
);
GO

-- 人ごとに 1 件 (同じ日に入れ直したときは、この行を使い回す。論理削除された行も含めて一意)。作業の累計の計算し直しにも使う
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkRecord_WorkItemId_Date_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE UNIQUE INDEX UX_WorkRecord_WorkItemId_Date_CreatedByUserId ON dbo.WorkRecord (WorkItemId, Date, CreatedByUserId);
GO

-- その日の記録の読み込み (削除されていない行だけ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkRecord_ProjectId_Date' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE INDEX IX_WorkRecord_ProjectId_Date ON dbo.WorkRecord (ProjectId, Date) WHERE IsDeleted = 0;
GO

-- 差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkRecord_ProjectId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE INDEX IX_WorkRecord_ProjectId_UpdatedAt ON dbo.WorkRecord (ProjectId, UpdatedAt);
GO
