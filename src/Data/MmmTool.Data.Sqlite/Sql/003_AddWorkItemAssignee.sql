-- 作業 (WorkItem)に、担当者の列 (AssigneeUserId)を足す。設計は docs/specs/work-items.md の「担当者」。
-- 列の並びを、画面の並び (名前 → 担当 → 優先度 → 日付 → 予定・実績 → 状態・進捗度 → 備考)に合わせるため、002 と同じく WorkItem を作り直す (ALTER TABLE ADD COLUMN は末尾にしか足せない)。
-- 0 は未割り当て。ローカルモードのユーザーは Id = 1 の固定値なので、これまでの作業は、すべて 1 の担当にする。
-- データベースの版が 2 のときだけ、アプリが 1 つのトランザクションで実行する。
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
    PlannedHours     REAL    NOT NULL DEFAULT 0 CHECK (PlannedHours >= 0 AND PlannedHours <= 9999),
    ActualTotalHours REAL    NOT NULL DEFAULT 0 CHECK (ActualTotalHours >= 0),
    StatusCodeNo     INTEGER NOT NULL DEFAULT 1 CHECK (StatusCodeNo >= 1),
    ProgressCodeNo   INTEGER NOT NULL DEFAULT 1 CHECK (ProgressCodeNo >= 1),
    Note             TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000),
    CompletedOn      TEXT    NOT NULL DEFAULT '9999-12-31'
);

INSERT INTO WorkItem_New (Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, AssigneeUserId,
                          PriorityCodeNo, StartDate, DueDate, PlannedHours, ActualTotalHours, StatusCodeNo, ProgressCodeNo, Note, CompletedOn)
SELECT Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, 1,
       PriorityCodeNo, StartDate, DueDate, PlannedHours, ActualTotalHours, StatusCodeNo, ProgressCodeNo, Note, CompletedOn
FROM WorkItem;

DROP TABLE WorkItem;
ALTER TABLE WorkItem_New RENAME TO WorkItem;

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IX_WorkItem_ProjectId_UpdatedAt ON WorkItem (ProjectId, UpdatedAt);

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IX_WorkItem_ProjectId_CompletedOn ON WorkItem (ProjectId, CompletedOn) WHERE IsDeleted = 0;
