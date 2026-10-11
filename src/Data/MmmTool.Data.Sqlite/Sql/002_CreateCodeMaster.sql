-- コードマスタ (CodeMaster) を作り、作業の状態・進捗度・優先度を、コード番号で持つ形にする。
-- 設計は docs/specs/work-items.md の「コードマスタ」と docs/specs/database.md。
-- やること: ① CodeMaster を作って初期値を入れる ② WorkItem を作り直す (列名と CHECK を変え、進捗度を % の値からコード番号 (% / 10 + 1)に変える)
--           ③ 使わなくなった WorkProgress を捨てる。データベースの版が 1 のときだけ、アプリが 1 つのトランザクションで実行する。
-- 型は 001 と同じ。外部キーは使わない。NULL は使わない。

-- コードマスタ。(FeatureNo, CategoryNo, CodeNo) で 1 件。FeatureNo は機能番号 (台帳は MmmTool.CodeMaster.Core の CodeFeatures)、
-- CategoryNo は機能の中の区分 (コードの種類)、CodeNo は区分の中の識別子 (一度使った番号は、同じ意味のまま変えない)
-- NumericValue は計算に使う数 (進捗度の % など。要らないコードは 0)、Name の「ない」は空文字
CREATE TABLE CodeMaster
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    FeatureNo       INTEGER NOT NULL CHECK (FeatureNo >= 1),
    CategoryNo      INTEGER NOT NULL CHECK (CategoryNo >= 1),
    CodeNo          INTEGER NOT NULL CHECK (CodeNo >= 1),
    SortOrder       INTEGER NOT NULL DEFAULT 0 CHECK (SortOrder >= 0),
    NumericValue    INTEGER NOT NULL DEFAULT 0,
    Name            TEXT    NOT NULL DEFAULT '' CHECK (length(Name) <= 100)
);

-- 論理削除された行も含めて一意 (選択肢から外した行を、同じコード番号で戻せる)
CREATE UNIQUE INDEX UX_CodeMaster_FeatureNo_CategoryNo_CodeNo ON CodeMaster (FeatureNo, CategoryNo, CodeNo);

-- 機能ごとの読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IX_CodeMaster_FeatureNo_UpdatedAt ON CodeMaster (FeatureNo, UpdatedAt);

-- 初期値 (作業管理 = 機能番号 2)。作成者・更新者は 1 (ローカルのユーザー。001 の初期値と同じ)
-- 区分 1: 状態 / 区分 2: 進捗度 (WorkProgress のラベルを引き継ぐ) / 区分 3: 優先度
INSERT INTO CodeMaster (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       2, v.CategoryNo, v.CodeNo, v.CodeNo, 0, v.Name
FROM (
    SELECT 1 AS CategoryNo, 1 AS CodeNo, '未対応' AS Name UNION ALL SELECT 1, 2, '作業中' UNION ALL SELECT 1, 3, '完了'
    UNION ALL SELECT 3, 1, '高' UNION ALL SELECT 3, 2, '中' UNION ALL SELECT 3, 3, '低'
) AS v;

INSERT INTO CodeMaster (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       2, 2, p.Value / 10 + 1, p.Value / 10 + 1, p.Value, p.Label
FROM WorkProgress p
WHERE p.IsDeleted = 0;

-- 作業を作り直す。Priority → PriorityCodeNo、Status → StatusCodeNo、Progress (% の値) → ProgressCodeNo (コード番号)。
-- CHECK は「1 以上」だけにする (選択肢が増えても、表を作り直さない)
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
    Note             TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000),
    PriorityCodeNo   INTEGER NOT NULL DEFAULT 2 CHECK (PriorityCodeNo >= 1),
    StartDate        TEXT    NOT NULL DEFAULT '0001-01-01',
    DueDate          TEXT    NOT NULL DEFAULT '9999-12-31',
    StatusCodeNo     INTEGER NOT NULL DEFAULT 1 CHECK (StatusCodeNo >= 1),
    ProgressCodeNo   INTEGER NOT NULL DEFAULT 1 CHECK (ProgressCodeNo >= 1),
    PlannedHours     REAL    NOT NULL DEFAULT 0 CHECK (PlannedHours >= 0 AND PlannedHours <= 9999),
    CompletedOn      TEXT    NOT NULL DEFAULT '9999-12-31',
    ActualTotalHours REAL    NOT NULL DEFAULT 0 CHECK (ActualTotalHours >= 0)
);

INSERT INTO WorkItem_New (Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, Note,
                          PriorityCodeNo, StartDate, DueDate, StatusCodeNo, ProgressCodeNo, PlannedHours, CompletedOn, ActualTotalHours)
SELECT Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, Note,
       Priority, StartDate, DueDate, Status, Progress / 10 + 1, PlannedHours, CompletedOn, ActualTotalHours
FROM WorkItem;

DROP TABLE WorkItem;
ALTER TABLE WorkItem_New RENAME TO WorkItem;

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IX_WorkItem_ProjectId_UpdatedAt ON WorkItem (ProjectId, UpdatedAt);

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IX_WorkItem_ProjectId_CompletedOn ON WorkItem (ProjectId, CompletedOn) WHERE IsDeleted = 0;

-- 進捗度のマスターは、コードマスタに移った
DROP TABLE WorkProgress;
