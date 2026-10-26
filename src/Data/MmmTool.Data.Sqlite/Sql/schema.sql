-- ローカルモード用の SQLite の、あるべき表と索引の形。起動のたびに、実際の DB と比べて、足りないものを足す。点検は SDK の SqliteSchemaRepair が行う。
-- 列・索引・表を足す変更は、番号つきのスクリプトではなく、このファイルに書き足す。何度流しても壊れないよう、IF NOT EXISTS を付ける。
-- 列の追加は、実際の DB の表の末尾に付く。型の変更・削除・データの書き換えは、ここでは直せないので、番号つきのスクリプトで行う。列の名前の変更は、SqliteMigrations.ColumnRenames に足す。
-- 初期データのコードマスタは、001_CreateTables.sql が入れる。ここには書かない。
-- 表の中にコメントは書かない。列の定義を取り出すとき、定義の一部になるため。コメントは、表の上の行に書く。

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

-- グループ。案件やグループの下に置く、子を持つ行
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

-- 作業。GroupId の 0 は案件の直下。日付のない日は、開始日が 0001-01-01、期限日が 9999-12-31、完了日の 9999-12-31 は未完了
-- ReportExclusion は、作業報告のどの部分から外すか。0 は外さない、1 は予定から外して実績には出す、2 は実績と予定の両方から外す
-- AutoHours は、毎日決まった工数を自動で入れる設定の時間で、0 は自動入力しない
-- AutoWeekdays は入れる曜日で、月 = 1 から日 = 64 の足し算の数。0 と 127 は、どちらも毎日
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
    ReportExclusion  INTEGER NOT NULL DEFAULT 0 CHECK (ReportExclusion IN (0, 1, 2)),
    AutoHours        REAL    NOT NULL DEFAULT 0 CHECK (AutoHours >= 0 AND AutoHours <= 24),
    AutoWeekdays     INTEGER NOT NULL DEFAULT 0 CHECK (AutoWeekdays BETWEEN 0 AND 127),
    StatusCodeNo    INTEGER NOT NULL DEFAULT 1 CHECK (StatusCodeNo >= 1),
    ProgressCodeNo   INTEGER NOT NULL DEFAULT 1 CHECK (ProgressCodeNo >= 1),
    Note             TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 1000)
);

-- 日ごとの記録。作業ごと・日ごとに 1 件で、人は区別しない。ProjectId は作業の案件で、選んだ案件に絞って読むために持つ
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

-- コードマスタ。全員で共通の選択肢
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

-- 日報メモ。1 人 1 日 1 件
CREATE TABLE IF NOT EXISTS WorkDayNote
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    UserId          INTEGER NOT NULL CHECK (UserId >= 1),
    Date            TEXT    NOT NULL,
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 2000)
);

-- ノート
CREATE TABLE IF NOT EXISTS Note
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    AuthorUserId    INTEGER NOT NULL CHECK (AuthorUserId >= 1),
    IsPrivate       INTEGER NOT NULL DEFAULT 0 CHECK (IsPrivate IN (0, 1)),
    StartDate       TEXT    NOT NULL,
    EndDate         TEXT    NOT NULL DEFAULT '9999-12-31' CHECK (EndDate >= StartDate),
    EventDate      TEXT    NOT NULL DEFAULT '9999-12-31' CHECK (EventDate = '9999-12-31' OR (EventDate >= StartDate AND EventDate <= EndDate)),
    Priority        INTEGER NOT NULL DEFAULT 3 CHECK (Priority BETWEEN 1 AND 5),
    Title           TEXT    NOT NULL CHECK (length(Title) BETWEEN 1 AND 100),
    Body            TEXT    NOT NULL DEFAULT ''
);

-- 案件の差分の取得
CREATE INDEX IF NOT EXISTS IX_WorkProject_UpdatedAt ON WorkProject (UpdatedAt);

-- 選んだ案件のグループの読み込み・差分の取得
CREATE INDEX IF NOT EXISTS IX_WorkGroup_ProjectId_UpdatedAt ON WorkGroup (ProjectId, UpdatedAt);

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
CREATE INDEX IF NOT EXISTS IX_WorkItem_ProjectId_UpdatedAt ON WorkItem (ProjectId, UpdatedAt);

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkItem_ProjectId_CompletedDate ON WorkItem (ProjectId, CompletedDate) WHERE IsDeleted = 0;

-- 作業報告の実績・予定で、担当が自分の作業を探す。削除されていない行だけ
CREATE INDEX IF NOT EXISTS IX_WorkItem_AssigneeUserId_StatusCodeNo ON WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;

-- 日ごとの記録を、作業ごと・日ごとに 1 件にする
CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkRecord_WorkItemId_Date ON WorkRecord (WorkItemId, Date);

-- 選んだ案件の日ごとの記録の読み込み (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkRecord_ProjectId_Date ON WorkRecord (ProjectId, Date) WHERE IsDeleted = 0;

-- 選んだ案件の記録の差分の取得
CREATE INDEX IF NOT EXISTS IX_WorkRecord_ProjectId_UpdatedAt ON WorkRecord (ProjectId, UpdatedAt);

-- コードマスタの 1 件を決める
CREATE UNIQUE INDEX IF NOT EXISTS UX_CodeMaster_FeatureNo_CategoryNo_CodeNo ON CodeMaster (FeatureNo, CategoryNo, CodeNo);

-- コードマスタの差分の取得
CREATE INDEX IF NOT EXISTS IX_CodeMaster_FeatureNo_UpdatedAt ON CodeMaster (FeatureNo, UpdatedAt);

-- 日報メモを、1 人 1 日 1 件にする
CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkDayNote_UserId_Date ON WorkDayNote (UserId, Date);

-- ノートの期間の読み込み
CREATE INDEX IF NOT EXISTS IX_Note_EndDate_StartDate ON Note (EndDate, StartDate);

-- 実際の今日が実施日のノートを探す (未設定の行を走査しない)
CREATE INDEX IF NOT EXISTS IX_Note_EventDate ON Note (EventDate);

-- ノートの「全員」の人の一覧で、予定と期間内に完了した作業の担当者を、表を読まずに探す (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId ON WorkItem (StatusCodeNo, CompletedDate, AssigneeUserId) WHERE IsDeleted = 0;

-- ノートの「全員」の人の一覧で、期間内に記録がある作業を日付から探す (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkRecord_Date_WorkItemId ON WorkRecord (Date, WorkItemId) WHERE IsDeleted = 0;

-- 自動入力の設定がある作業を、担当者から探す。大半の作業は設定が無いので、設定のある作業だけを入れる
CREATE INDEX IF NOT EXISTS IX_WorkItem_AssigneeUserId_AutoHours ON WorkItem (AssigneeUserId) WHERE IsDeleted = 0 AND AutoHours > 0;

-- ノートの「全員」の人の一覧で、報告日に日報メモがある人を日付から探す (削除されていない行だけ)
CREATE INDEX IF NOT EXISTS IX_WorkDayNote_Date_UserId ON WorkDayNote (Date, UserId) WHERE IsDeleted = 0;
