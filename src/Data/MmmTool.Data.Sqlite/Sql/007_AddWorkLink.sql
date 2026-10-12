-- 案件 (WorkProject)・グループ (WorkGroup)・作業 (WorkItem)に、リンクの列 (Link)を足す。設計は docs/specs/work-items.md の「リンク」。
-- Link は、URL・フォルダ・ファイルのパス (1 行に 1 つ。2000 文字まで)。無いときは空文字 (NULL は使わない)。
-- 列の並びを設計した位置 (案件・グループは Name → Link → Note、作業は ActualTotalHours → Link → StatusCodeNo)にするため、003 と同じく表を作り直す (ALTER TABLE ADD COLUMN は末尾にしか足せない)。
-- データベースの版が 6 のときだけ、アプリが 1 つのトランザクションで実行する。今ある行の Link は空文字。

-- 案件
CREATE TABLE WorkProject_New
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    Name            TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 200),
    Link            TEXT    NOT NULL DEFAULT '' CHECK (length(Link) <= 2000),
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

INSERT INTO WorkProject_New (Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, Name, Link, Note)
SELECT Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, Name, '', Note
FROM WorkProject;

DROP TABLE WorkProject;
ALTER TABLE WorkProject_New RENAME TO WorkProject;

-- 差分の取得 (論理削除された行も含む)
CREATE INDEX IX_WorkProject_UpdatedAt ON WorkProject (UpdatedAt);

-- グループ
CREATE TABLE WorkGroup_New
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    ProjectId       INTEGER NOT NULL,
    ParentGroupId   INTEGER NOT NULL DEFAULT 0 CHECK (ParentGroupId >= 0),
    SortOrder       INTEGER NOT NULL DEFAULT 0 CHECK (SortOrder >= 0),
    Name            TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 200),
    Link            TEXT    NOT NULL DEFAULT '' CHECK (length(Link) <= 2000),
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

INSERT INTO WorkGroup_New (Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, ParentGroupId, SortOrder, Name, Link, Note)
SELECT Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, ParentGroupId, SortOrder, Name, '', Note
FROM WorkGroup;

DROP TABLE WorkGroup;
ALTER TABLE WorkGroup_New RENAME TO WorkGroup;

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IX_WorkGroup_ProjectId_UpdatedAt ON WorkGroup (ProjectId, UpdatedAt);

-- 作業
CREATE TABLE WorkItem_New
(
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted        INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt        TEXT    NOT NULL,
    CreatedByUserId  INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt        TEXT    NOT NULL,
    UpdatedByUserId  INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    ProjectId        INTEGER NOT NULL,
    GroupId          INTEGER NOT NULL DEFAULT 0 CHECK (GroupId >= 0),
    SortOrder        INTEGER NOT NULL DEFAULT 0 CHECK (SortOrder >= 0),
    Name             TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 200),
    AssigneeUserId   INTEGER NOT NULL DEFAULT 0 CHECK (AssigneeUserId >= 0),
    PriorityCodeNo   INTEGER NOT NULL DEFAULT 2 CHECK (PriorityCodeNo >= 1),
    StartDate        TEXT    NOT NULL DEFAULT '0001-01-01',
    DueDate          TEXT    NOT NULL DEFAULT '9999-12-31',
    CompletedDate    TEXT    NOT NULL DEFAULT '9999-12-31',
    PlannedHours     REAL    NOT NULL DEFAULT 0 CHECK (PlannedHours >= 0 AND PlannedHours <= 9999),
    ActualTotalHours REAL    NOT NULL DEFAULT 0 CHECK (ActualTotalHours >= 0),
    Link             TEXT    NOT NULL DEFAULT '' CHECK (length(Link) <= 2000),
    StatusCodeNo     INTEGER NOT NULL DEFAULT 1 CHECK (StatusCodeNo >= 1),
    ProgressCodeNo   INTEGER NOT NULL DEFAULT 1 CHECK (ProgressCodeNo >= 1),
    Note             TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

INSERT INTO WorkItem_New (Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, AssigneeUserId,
                          PriorityCodeNo, StartDate, DueDate, CompletedDate, PlannedHours, ActualTotalHours, Link, StatusCodeNo, ProgressCodeNo, Note)
SELECT Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, AssigneeUserId,
       PriorityCodeNo, StartDate, DueDate, CompletedDate, PlannedHours, ActualTotalHours, '', StatusCodeNo, ProgressCodeNo, Note
FROM WorkItem;

DROP TABLE WorkItem;
ALTER TABLE WorkItem_New RENAME TO WorkItem;

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IX_WorkItem_ProjectId_UpdatedAt ON WorkItem (ProjectId, UpdatedAt);

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IX_WorkItem_ProjectId_CompletedDate ON WorkItem (ProjectId, CompletedDate) WHERE IsDeleted = 0;

-- 作業報告の実績・予定 (担当が自分の作業を探す。削除されていない行だけ)
CREATE INDEX IX_WorkItem_AssigneeUserId_StatusCodeNo ON WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;
