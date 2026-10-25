-- MmmTool のローカルの DB (SQLite。Data/MmmTool.db)。作業管理の 5 つの表と、コードマスタを作る。
-- 設計は docs/specs/work-items.md と docs/specs/database.md。何度流しても壊れない (無いものだけを作る)。
-- アプリが、データベースを開くとき、PRAGMA user_version より新しい番号のスクリプトを順に実行する。
-- 型: 日時は TEXT (2026-10-11T14:30:00.123+09:00。同じ時差なら文字の大小が前後になる)、日付は TEXT (2026-10-11。ない日は 9999-12-31)、
--     時間は REAL (合計は書くたびに ROUND(…, 2) で丸める)、真偽は INTEGER (0 か 1)。外部キーは使わない。NULL は使わない。

-- 案件マスター。全員で共有する案件の 1 件。Link は URL・フォルダ・ファイルのパス (無いときは空文字)
CREATE TABLE IF NOT EXISTS WorkProject
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
    Link            TEXT    NOT NULL DEFAULT '' CHECK (length(Link) <= 2000),
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkGroup_ProjectId_UpdatedAt ON WorkGroup (ProjectId, UpdatedAt);

-- 作業。GroupId の 0 は案件の直下。AssigneeUserId の 0 は未割り当て。日付のない日は 9999-12-31 (開始日は 0001-01-01)、PlannedHours の 0 は予定なし、CompletedDate の 9999-12-31 は未完了
-- ActualTotalHours は、その作業の全員の記録の合計 (記録を保存するたびに計算し直して書く)
-- PriorityCodeNo・StatusCodeNo・ProgressCodeNo は、コードマスタ (CodeMaster)のコード番号 (区分 3・1・2)
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

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkItem_ProjectId_UpdatedAt ON WorkItem (ProjectId, UpdatedAt);

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkItem_ProjectId_CompletedDate ON WorkItem (ProjectId, CompletedDate) WHERE IsDeleted = 0;

-- 作業報告の実績・予定 (担当が自分の作業を探す。削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkItem_AssigneeUserId_StatusCodeNo ON WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;

-- 日ごとの記録。作業ごと・日ごとに 1 件 (人は区別しない)。ProjectId は作業の案件 (選んだ案件に絞って読むため)
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

-- 作業ごと・日ごとに 1 件 (同じ日に入れ直したときは、この行を使い回す。論理削除された行も含めて一意)。作業の累計の計算し直しにも使う
CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkRecord_WorkItemId_Date ON WorkRecord (WorkItemId, Date);

-- その日の記録の読み込み (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkRecord_ProjectId_Date ON WorkRecord (ProjectId, Date) WHERE IsDeleted = 0;

-- 差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkRecord_ProjectId_UpdatedAt ON WorkRecord (ProjectId, UpdatedAt);

-- コードマスタ。(FeatureNo, CategoryNo, CodeNo) で 1 件。FeatureNo は機能番号 (台帳は MmmTool.CodeMaster.Core の CodeFeatures)、
-- CategoryNo は機能の中の区分 (コードの種類)、CodeNo は区分の中の識別子 (一度使った番号は、同じ意味のまま変えない)
-- NumericValue は計算に使う数 (進捗度の % など。要らないコードは 0)、Name の「ない」は空文字
CREATE TABLE IF NOT EXISTS CodeMaster
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
CREATE UNIQUE INDEX IF NOT EXISTS UX_CodeMaster_FeatureNo_CategoryNo_CodeNo ON CodeMaster (FeatureNo, CategoryNo, CodeNo);

-- 機能ごとの読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_CodeMaster_FeatureNo_UpdatedAt ON CodeMaster (FeatureNo, UpdatedAt);

-- 初期値 (作業管理 = 機能番号 2)。作成者・更新者は 1 (ローカルのユーザー)。無いコードだけを入れる
-- 区分 1: 状態 (未対応・作業予定・作業中・完了) / 区分 2: 進捗度 (コード番号 1〜11 が 0%〜100%。名前は、すべて空) / 区分 3: 優先度
INSERT INTO CodeMaster (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       2, v.CategoryNo, v.CodeNo, v.CodeNo, v.NumericValue, v.Name
FROM (
    SELECT 1 AS CategoryNo, 1 AS CodeNo, 0 AS NumericValue, '未対応' AS Name
    UNION ALL SELECT 1, 2, 0, '作業予定' UNION ALL SELECT 1, 3, 0, '作業中' UNION ALL SELECT 1, 4, 0, '完了'
    UNION ALL SELECT 2, 1, 0, '' UNION ALL SELECT 2, 2, 10, '' UNION ALL SELECT 2, 3, 20, '' UNION ALL SELECT 2, 4, 30, '' UNION ALL SELECT 2, 5, 40, ''
    UNION ALL SELECT 2, 6, 50, '' UNION ALL SELECT 2, 7, 60, '' UNION ALL SELECT 2, 8, 70, '' UNION ALL SELECT 2, 9, 80, ''
    UNION ALL SELECT 2, 10, 90, '' UNION ALL SELECT 2, 11, 100, ''
    UNION ALL SELECT 3, 1, 0, '高' UNION ALL SELECT 3, 2, 0, '中' UNION ALL SELECT 3, 3, 0, '低'
) AS v
WHERE NOT EXISTS (SELECT 1 FROM CodeMaster c WHERE c.FeatureNo = 2 AND c.CategoryNo = v.CategoryNo AND c.CodeNo = v.CodeNo);
