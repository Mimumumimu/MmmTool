-- MmmTool の DB (SQL Server)。作業リストの 6 つの表を、dbo スキーマに作る。
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

-- 作業。GroupId の 0 は案件の直下。日付のない日は 9999-12-31、PlannedHours の 0 は予定なし、CompletedOn の 9999-12-31 は未完了
-- ActualTotalHours は、その作業の全員の記録の合計 (記録を保存するたびに計算し直して書く)
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
    Note             nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkItem_Note DEFAULT (N''),
    Priority         tinyint         NOT NULL CONSTRAINT DF_WorkItem_Priority DEFAULT (2) CONSTRAINT CK_WorkItem_Priority CHECK (Priority BETWEEN 1 AND 3),
    StartDate        date            NOT NULL CONSTRAINT DF_WorkItem_StartDate DEFAULT ('9999-12-31'),
    DueDate          date            NOT NULL CONSTRAINT DF_WorkItem_DueDate DEFAULT ('9999-12-31'),
    Status           tinyint         NOT NULL CONSTRAINT DF_WorkItem_Status DEFAULT (1) CONSTRAINT CK_WorkItem_Status CHECK (Status BETWEEN 1 AND 3),
    Progress         tinyint         NOT NULL CONSTRAINT DF_WorkItem_Progress DEFAULT (0) CONSTRAINT CK_WorkItem_Progress CHECK (Progress BETWEEN 0 AND 100),
    PlannedHours     decimal(6, 2)   NOT NULL CONSTRAINT DF_WorkItem_PlannedHours DEFAULT (0) CONSTRAINT CK_WorkItem_PlannedHours CHECK (PlannedHours >= 0),
    CompletedOn      date            NOT NULL CONSTRAINT DF_WorkItem_CompletedOn DEFAULT ('9999-12-31'),
    ActualTotalHours decimal(12, 2)  NOT NULL CONSTRAINT DF_WorkItem_ActualTotalHours DEFAULT (0) CONSTRAINT CK_WorkItem_ActualTotalHours CHECK (ActualTotalHours >= 0)
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

-- 利用者が、表示する案件に選んだ記録。作成者・更新者は UserId と同じ人。外したら IsDeleted にし、選び直したら同じ行を使い回す
IF OBJECT_ID(N'dbo.WorkProjectSelection', N'U') IS NULL
CREATE TABLE dbo.WorkProjectSelection
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkProjectSelection PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_WorkProjectSelection_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkProjectSelection_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_WorkProjectSelection_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkProjectSelection_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_WorkProjectSelection_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    UserId          int             NOT NULL CONSTRAINT CK_WorkProjectSelection_UserId CHECK (UserId >= 0),
    ProjectId       int             NOT NULL
);
GO

-- 削除されていない行の間だけ一意
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkProjectSelection_UserId_ProjectId' AND object_id = OBJECT_ID(N'dbo.WorkProjectSelection'))
CREATE UNIQUE INDEX UX_WorkProjectSelection_UserId_ProjectId ON dbo.WorkProjectSelection (UserId, ProjectId) WHERE IsDeleted = 0;
GO

-- 別の PC で選びを変えたことを、差分で知る (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkProjectSelection_UserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkProjectSelection'))
CREATE INDEX IX_WorkProjectSelection_UserId_UpdatedAt ON dbo.WorkProjectSelection (UserId, UpdatedAt);
GO

-- 進捗度のマスター。値は 0 から 100 の 10 刻みで固定 (11 個)。持つのは値ごとのラベルだけ。全員に影響するので、画面からは編集しない (この表を SQL で直す)
IF OBJECT_ID(N'dbo.WorkProgress', N'U') IS NULL
CREATE TABLE dbo.WorkProgress
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkProgress PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_WorkProgress_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkProgress_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_WorkProgress_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkProgress_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_WorkProgress_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    Value           tinyint         NOT NULL CONSTRAINT CK_WorkProgress_Value CHECK (Value BETWEEN 0 AND 100 AND Value % 10 = 0),
    Label           nvarchar(50)    NOT NULL CONSTRAINT DF_WorkProgress_Label DEFAULT (N'')
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkProgress_Value' AND object_id = OBJECT_ID(N'dbo.WorkProgress'))
CREATE UNIQUE INDEX UX_WorkProgress_Value ON dbo.WorkProgress (Value);
GO

-- 初期値 (11 個。70・80・90 だけラベルがある)。無い値だけを入れる。作成者・更新者は 0 (特定のユーザーではない)
INSERT INTO dbo.WorkProgress (IsDeleted, CreatedByUserId, UpdatedByUserId, Value, Label)
SELECT 0, 0, 0, v.Value, v.Label
FROM (VALUES
    (0, N''), (10, N''), (20, N''), (30, N''), (40, N''), (50, N''), (60, N''),
    (70, N'作成完了'), (80, N'内部レビュー完了'), (90, N'外部レビュー完了'), (100, N'')
) AS v (Value, Label)
WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkProgress p WHERE p.Value = v.Value);
GO
