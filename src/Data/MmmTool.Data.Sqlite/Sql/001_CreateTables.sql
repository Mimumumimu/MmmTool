-- MmmTool のローカルの DB (SQLite。Data/MmmTool.db)。作業管理の 6 つの表を作る。
-- 設計は docs/specs/work-items.md と docs/specs/database.md。何度流しても壊れない (無いものだけを作る)。
-- アプリが、データベースを開くとき、PRAGMA user_version より新しい番号のスクリプトを順に実行する。
-- 型: 日時は TEXT (2026-10-11T14:30:00.123+09:00。同じ時差なら文字の大小が前後になる)、日付は TEXT (2026-10-11。ない日は 9999-12-31)、
--     時間は REAL (合計は書くたびに ROUND(…, 2) で丸める)、真偽は INTEGER (0 か 1)。外部キーは使わない。NULL は使わない。

-- 案件マスター。全員で共有する案件の 1 件
CREATE TABLE IF NOT EXISTS WorkProject
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    Name            TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 200),
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

-- 差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkProject_UpdatedAt ON WorkProject (UpdatedAt);

-- グループ。ParentGroupId の 0 は案件の直下。SortOrder は、グループと作業にまたがる、兄弟の中の並び
CREATE TABLE IF NOT EXISTS WorkGroup
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
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkGroup_ProjectId_UpdatedAt ON WorkGroup (ProjectId, UpdatedAt);

-- 作業。GroupId の 0 は案件の直下。日付のない日は 9999-12-31、PlannedHours の 0 は予定なし、CompletedOn の 9999-12-31 は未完了
-- ActualTotalHours は、その作業の全員の記録の合計 (記録を保存するたびに計算し直して書く)
CREATE TABLE IF NOT EXISTS WorkItem
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
    Priority         INTEGER NOT NULL DEFAULT 2 CHECK (Priority BETWEEN 1 AND 3),
    StartDate        TEXT    NOT NULL DEFAULT '0001-01-01',
    DueDate          TEXT    NOT NULL DEFAULT '9999-12-31',
    Status           INTEGER NOT NULL DEFAULT 1 CHECK (Status BETWEEN 1 AND 3),
    Progress         INTEGER NOT NULL DEFAULT 0 CHECK (Progress BETWEEN 0 AND 100),
    PlannedHours     REAL    NOT NULL DEFAULT 0 CHECK (PlannedHours >= 0 AND PlannedHours <= 9999),
    CompletedOn      TEXT    NOT NULL DEFAULT '9999-12-31',
    ActualTotalHours REAL    NOT NULL DEFAULT 0 CHECK (ActualTotalHours >= 0)
);

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkItem_ProjectId_UpdatedAt ON WorkItem (ProjectId, UpdatedAt);

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkItem_ProjectId_CompletedOn ON WorkItem (ProjectId, CompletedOn) WHERE IsDeleted = 0;

-- 日ごとの記録。CreatedByUserId が記録した人 (人ごとに 1 件)。ProjectId は作業の案件 (選んだ案件に絞って読むため)
CREATE TABLE IF NOT EXISTS WorkRecord
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    ProjectId       INTEGER NOT NULL,
    WorkItemId      INTEGER NOT NULL,
    Date            TEXT    NOT NULL,
    Hours           REAL    NOT NULL DEFAULT 0 CHECK (Hours >= 0 AND Hours <= 24),
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

-- 人ごとに 1 件 (同じ日に入れ直したときは、この行を使い回す。論理削除された行も含めて一意)。作業の累計の計算し直しにも使う
CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkRecord_WorkItemId_Date_CreatedByUserId ON WorkRecord (WorkItemId, Date, CreatedByUserId);

-- その日の記録の読み込み (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkRecord_ProjectId_Date ON WorkRecord (ProjectId, Date) WHERE IsDeleted = 0;

-- 差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkRecord_ProjectId_UpdatedAt ON WorkRecord (ProjectId, UpdatedAt);

-- 利用者が、表示する案件に選んだ記録。作成者・更新者は UserId と同じ人。外したら IsDeleted にし、選び直したら同じ行を使い回す
CREATE TABLE IF NOT EXISTS WorkProjectSelection
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    UserId          INTEGER NOT NULL CHECK (UserId >= 0),
    ProjectId       INTEGER NOT NULL
);

-- 削除されていない行の間だけ一意
CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkProjectSelection_UserId_ProjectId ON WorkProjectSelection (UserId, ProjectId) WHERE IsDeleted = 0;

-- 別の PC で選びを変えたことを、差分で知る (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkProjectSelection_UserId_UpdatedAt ON WorkProjectSelection (UserId, UpdatedAt);

-- 進捗度のマスター。値は 0 から 100 の 10 刻みで固定 (11 個)。持つのは値ごとのラベルだけ
CREATE TABLE IF NOT EXISTS WorkProgress
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    Value           INTEGER NOT NULL CHECK (Value BETWEEN 0 AND 100 AND Value % 10 = 0),
    Label           TEXT    NOT NULL DEFAULT '' CHECK (length(Label) <= 50)
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkProgress_Value ON WorkProgress (Value);

-- 初期値 (11 個。70・80・90 だけラベルがある)。初めて作るときだけ入る。作成者・更新者は 1 (ローカルのユーザー)
INSERT OR IGNORE INTO WorkProgress (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, Value, Label)
SELECT 0,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       v.Value, v.Label
FROM (
    SELECT 0 AS Value, '' AS Label UNION ALL SELECT 10, '' UNION ALL SELECT 20, '' UNION ALL SELECT 30, '' UNION ALL SELECT 40, ''
    UNION ALL SELECT 50, '' UNION ALL SELECT 60, '' UNION ALL SELECT 70, '作成完了' UNION ALL SELECT 80, '内部レビュー完了'
    UNION ALL SELECT 90, '外部レビュー完了' UNION ALL SELECT 100, ''
) AS v;
