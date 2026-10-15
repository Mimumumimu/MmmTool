-- MmmTool の DB (SQL Server)。dbo スキーマに、MmmTool が使う全部の表を作り、コードマスタの初期値を入れる。この 1 本が、この版の全部の DDL。
-- 設計は docs/specs/database.md と docs/specs/work-items.md。何度流しても壊れない (無いものだけを作る・入れる)。DELETE 文は含まない。
-- 内容: ① ユーザー・リマインダー ② 送信先と送信設定 (リマインダーの送信 (MmmBatch)用) ③ 作業管理 ④ コードマスタ ⑤ 作業管理のコードの初期値
-- 使い方: 先に DB を作り、その DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する。sqlcmd で流すときは、フィルターつきのインデックスのため、-I を付ける (SSMS は既定でオン)。日本語を含むので、ファイルは UTF-8 で、sqlcmd には -f 65001 を付ける。
-- 送信の状況の表 (ReminderSendStatus)は、MmmBatch 側の src/Data/MmmBatch.Data.SqlServer/Sql/001_CreateTables.sql が作る。

-- ユーザー。1 行が 1 人 (ログイン名とパスワードで特定する)。PasswordHash の空文字は、新しく決める状態 (管理者が再設定したあと)
IF OBJECT_ID(N'dbo.AppUser', N'U') IS NULL
CREATE TABLE dbo.AppUser
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_AppUser_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_AppUser_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUser_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_AppUser_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    DisplayName     nvarchar(50)    NOT NULL,
    LoginName       nvarchar(50)    COLLATE Latin1_General_100_CI_AS NOT NULL,
    PasswordHash    varchar(200)    NOT NULL CONSTRAINT DF_AppUser_PasswordHash DEFAULT (''),
    ValidFrom       date            NOT NULL,
    ValidTo         date            NOT NULL CONSTRAINT DF_AppUser_ValidTo DEFAULT ('9999-12-31')
);
GO

-- ログイン名は、削除されていない行の間だけ一意 (大文字と小文字は区別しない)。削除済みのユーザーのログイン名は、新しい人が使える
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUser_LoginName' AND object_id = OBJECT_ID(N'dbo.AppUser'))
CREATE UNIQUE INDEX UX_AppUser_LoginName ON dbo.AppUser (LoginName) WHERE IsDeleted = 0;
GO

-- リマインダー。作成者 AuthorUserId (作った人。見える範囲の条件に使う。共通ヘッダーの CreatedByUserId は条件に使わない)と、宛先 TargetUserId の 0 は全員宛て。人を表す 2 つの列を続けて並べる。Weekdays は月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数 (0 は曜日指定では毎日)
IF OBJECT_ID(N'dbo.Reminder', N'U') IS NULL
CREATE TABLE dbo.Reminder
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reminder PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_Reminder_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Reminder_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_Reminder_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Reminder_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_Reminder_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    AuthorUserId    int             NOT NULL CONSTRAINT DF_Reminder_AuthorUserId DEFAULT (0) CONSTRAINT CK_Reminder_AuthorUserId CHECK (AuthorUserId >= 0),
    TargetUserId    int             NOT NULL CONSTRAINT CK_Reminder_TargetUserId CHECK (TargetUserId >= 0),
    Date            date            NOT NULL CONSTRAINT DF_Reminder_Date DEFAULT ('9999-12-31'),
    Time            time(0)         NOT NULL,
    Weekdays        tinyint         NOT NULL CONSTRAINT DF_Reminder_Weekdays DEFAULT (0) CONSTRAINT CK_Reminder_Weekdays CHECK (Weekdays BETWEEN 0 AND 127),
    Title           nvarchar(200)   NOT NULL,
    Note            nvarchar(1000)  NOT NULL CONSTRAINT DF_Reminder_Note DEFAULT (N''),
    Link            nvarchar(2000)  NOT NULL CONSTRAINT DF_Reminder_Link DEFAULT (N''),
    IsSpeak         bit             NOT NULL CONSTRAINT DF_Reminder_IsSpeak DEFAULT (0)
);
GO

-- リマインダーの対応状態。人ごと・リマインダーごとに 1 件。None (未対応)は行を持たない
IF OBJECT_ID(N'dbo.ReminderState', N'U') IS NULL
CREATE TABLE dbo.ReminderState
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReminderState PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_ReminderState_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderState_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_ReminderState_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderState_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_ReminderState_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ReminderId      int             NOT NULL,
    UserId          int             NOT NULL,
    Date            date            NOT NULL,
    Status          tinyint         NOT NULL CONSTRAINT CK_ReminderState_Status CHECK (Status IN (1, 2)),
    CONSTRAINT UQ_ReminderState_ReminderId_UserId UNIQUE (ReminderId, UserId)
);
GO

-- 送信先 (ntfy・Discord)の登録。1 行が 1 つの送信先。使用者 UserId (この送信先を使う人。一覧・選択・更新・削除の条件に使う。共通ヘッダーの CreatedByUserId は条件に使わない)を、区分・名前・値より前に置く。Kind は ntfy = 1・Discord = 2。Value は ntfy のトピック名か、Discord の Webhook の URL (秘密)
IF OBJECT_ID(N'dbo.NotificationChannel', N'U') IS NULL
CREATE TABLE dbo.NotificationChannel
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationChannel PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_NotificationChannel_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NotificationChannel_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_NotificationChannel_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NotificationChannel_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_NotificationChannel_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    UserId          int             NOT NULL CONSTRAINT DF_NotificationChannel_UserId DEFAULT (0) CONSTRAINT CK_NotificationChannel_UserId CHECK (UserId >= 0),
    Kind            tinyint         NOT NULL CONSTRAINT CK_NotificationChannel_Kind CHECK (Kind IN (1, 2)),
    Name            nvarchar(50)    NOT NULL,
    Value           nvarchar(500)   NOT NULL
);
GO

-- リマインダーの送信設定。どのリマインダーを、どの送信先へ送るか。1 つのリマインダーに、送信先を複数選べる (1 つの送信先につき 1 行)。選ばなくなった送信先の行は、論理削除する
IF OBJECT_ID(N'dbo.ReminderSendSetting', N'U') IS NULL
CREATE TABLE dbo.ReminderSendSetting
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReminderSendSetting PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_ReminderSendSetting_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderSendSetting_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_ReminderSendSetting_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderSendSetting_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_ReminderSendSetting_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ReminderId      int             NOT NULL,
    ChannelId       int             NOT NULL
);
GO

-- 同じリマインダーに、同じ送信先を重ねない (削除されていない行の間だけ一意)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ReminderSendSetting_ReminderId_ChannelId' AND object_id = OBJECT_ID(N'dbo.ReminderSendSetting'))
CREATE UNIQUE INDEX UX_ReminderSendSetting_ReminderId_ChannelId ON dbo.ReminderSendSetting (ReminderId, ChannelId) WHERE IsDeleted = 0;
GO

-- メッセージの送信依頼。MmmTool が行を足し (送信待ち)、MmmBatch が送って状況を更新する。1 回の送信の操作が 1 行 (一意制約は付けない)。
-- Status は Pending = 1 (送信待ち・送信中)・Sent = 2・Failed = 3。SentAt の 9999-12-31 は、まだ送っていない。失敗の理由は持たない (MmmBatch のログに残す)。
-- 作成者・更新者は、依頼したときは送った人、MmmBatch が更新するときの更新者は 0
IF OBJECT_ID(N'dbo.MessageSendRequest', N'U') IS NULL
CREATE TABLE dbo.MessageSendRequest
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_MessageSendRequest PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_MessageSendRequest_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageSendRequest_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_MessageSendRequest_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageSendRequest_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_MessageSendRequest_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    SenderUserId    int             NOT NULL CONSTRAINT CK_MessageSendRequest_SenderUserId CHECK (SenderUserId >= 0),
    ChannelId       int             NOT NULL,
    Title           nvarchar(100)   NOT NULL CONSTRAINT DF_MessageSendRequest_Title DEFAULT (N''),
    Body            nvarchar(1000)  NOT NULL,
    Status          tinyint         NOT NULL CONSTRAINT DF_MessageSendRequest_Status DEFAULT (1) CONSTRAINT CK_MessageSendRequest_Status CHECK (Status BETWEEN 1 AND 3),
    Attempts        tinyint         NOT NULL CONSTRAINT DF_MessageSendRequest_Attempts DEFAULT (0),
    SentAt          datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageSendRequest_SentAt DEFAULT ('9999-12-31')
);
GO

-- MmmBatch が毎分読む送信待ち・失敗の行を、状態で探す (送信済みの大量の行を、毎分走査しないため)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageSendRequest_Status' AND object_id = OBJECT_ID(N'dbo.MessageSendRequest'))
CREATE INDEX IX_MessageSendRequest_Status ON dbo.MessageSendRequest (Status);
GO

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
    Link            nvarchar(2000)  NOT NULL CONSTRAINT DF_WorkProject_Link DEFAULT (N''),
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
    Link            nvarchar(2000)  NOT NULL CONSTRAINT DF_WorkGroup_Link DEFAULT (N''),
    Note            nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkGroup_Note DEFAULT (N'')
);
GO

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkGroup_ProjectId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkGroup'))
CREATE INDEX IX_WorkGroup_ProjectId_UpdatedAt ON dbo.WorkGroup (ProjectId, UpdatedAt);
GO

-- 作業。GroupId の 0 は案件の直下。日付のない日は、開始日が 0001-01-01、期限日が 9999-12-31、PlannedHours の 0 は予定なし、CompletedDate の 9999-12-31 は未完了
-- ActualTotalHours は、その作業の全員の記録の合計 (記録を保存するたびに計算し直して書く)
-- AssigneeUserId は担当者 (AppUser.Id。0 は未割り当て)
-- Link は、案件・グループ・作業に付ける 1 つのリンク (URL・フォルダ・ファイルのパス。無いときは空文字)
-- PriorityCodeNo・StatusCodeNo・ProgressCodeNo は、コードマスタ (下のコードマスタ CodeMaster)のコード番号 (区分 3・1・2)。外部キーは使わない
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
    CompletedDate    date            NOT NULL CONSTRAINT DF_WorkItem_CompletedDate DEFAULT ('9999-12-31'),
    PlannedHours     decimal(6, 2)   NOT NULL CONSTRAINT DF_WorkItem_PlannedHours DEFAULT (0) CONSTRAINT CK_WorkItem_PlannedHours CHECK (PlannedHours >= 0),
    ActualTotalHours decimal(12, 2)  NOT NULL CONSTRAINT DF_WorkItem_ActualTotalHours DEFAULT (0) CONSTRAINT CK_WorkItem_ActualTotalHours CHECK (ActualTotalHours >= 0),
    Link             nvarchar(2000)  NOT NULL CONSTRAINT DF_WorkItem_Link DEFAULT (N''),
    StatusCodeNo     int             NOT NULL CONSTRAINT DF_WorkItem_StatusCodeNo DEFAULT (1) CONSTRAINT CK_WorkItem_StatusCodeNo CHECK (StatusCodeNo >= 1),
    ProgressCodeNo   int             NOT NULL CONSTRAINT DF_WorkItem_ProgressCodeNo DEFAULT (1) CONSTRAINT CK_WorkItem_ProgressCodeNo CHECK (ProgressCodeNo >= 1),
    Note             nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkItem_Note DEFAULT (N'')
);
GO

-- 選んだ案件の中身の読み込み・差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_ProjectId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_ProjectId_UpdatedAt ON dbo.WorkItem (ProjectId, UpdatedAt);
GO

-- 案件の完了の計算 (削除されていない行だけ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_ProjectId_CompletedDate' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_ProjectId_CompletedDate ON dbo.WorkItem (ProjectId, CompletedDate) WHERE IsDeleted = 0;
GO

-- 作業報告の実績・予定 (担当が自分の作業を探す。削除されていない行だけ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_AssigneeUserId_StatusCodeNo' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_AssigneeUserId_StatusCodeNo ON dbo.WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;
GO

-- 日ごとの記録。作業ごと・日ごとに 1 件。ProjectId は作業の案件 (選んだ案件に絞って読むため)。Id は bigint
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

-- 作業ごと・日ごとに 1 件 (人は区別しない。同じ日に入れ直したときは、この行を使い回す。論理削除された行も含めて一意)。作業の累計の計算し直しにも使う
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkRecord_WorkItemId_Date' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE UNIQUE INDEX UX_WorkRecord_WorkItemId_Date ON dbo.WorkRecord (WorkItemId, Date);
GO

-- その日の記録の読み込み (削除されていない行だけ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkRecord_ProjectId_Date' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE INDEX IX_WorkRecord_ProjectId_Date ON dbo.WorkRecord (ProjectId, Date) WHERE IsDeleted = 0;
GO

-- 差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkRecord_ProjectId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE INDEX IX_WorkRecord_ProjectId_UpdatedAt ON dbo.WorkRecord (ProjectId, UpdatedAt);
GO

-- コードマスタ。(FeatureNo, CategoryNo, CodeNo) で 1 件。FeatureNo は機能番号 (台帳は MmmTool.CodeMaster.Core の CodeFeatures。作業管理は 2)、
-- CategoryNo は機能の中の区分 (コードの種類。作業管理は 1 = 状態・2 = 進捗度・3 = 優先度)、CodeNo は区分の中の識別子 (一度使った番号は、同じ意味のまま変えない)
-- NumericValue は計算に使う数 (進捗度の % など。要らないコードは 0)、Name の「ない」は空文字
-- 全員に影響するので、画面からは編集しない (この表を SQL で直す。選択肢を外すときは IsDeleted = 1。DELETE はしない)
IF OBJECT_ID(N'dbo.CodeMaster', N'U') IS NULL
CREATE TABLE dbo.CodeMaster
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodeMaster PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_CodeMaster_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_CodeMaster_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_CodeMaster_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_CodeMaster_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_CodeMaster_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    FeatureNo       int             NOT NULL CONSTRAINT CK_CodeMaster_FeatureNo CHECK (FeatureNo >= 1),
    CategoryNo      int             NOT NULL CONSTRAINT CK_CodeMaster_CategoryNo CHECK (CategoryNo >= 1),
    CodeNo          int             NOT NULL CONSTRAINT CK_CodeMaster_CodeNo CHECK (CodeNo >= 1),
    SortOrder       int             NOT NULL CONSTRAINT DF_CodeMaster_SortOrder DEFAULT (0) CONSTRAINT CK_CodeMaster_SortOrder CHECK (SortOrder >= 0),
    NumericValue    int             NOT NULL CONSTRAINT DF_CodeMaster_NumericValue DEFAULT (0),
    Name            nvarchar(100)   NOT NULL CONSTRAINT DF_CodeMaster_Name DEFAULT (N'')
);
GO

-- 論理削除された行も含めて一意 (選択肢から外した行を、同じコード番号で戻せる)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CodeMaster_FeatureNo_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.CodeMaster'))
CREATE UNIQUE INDEX UX_CodeMaster_FeatureNo_CategoryNo_CodeNo ON dbo.CodeMaster (FeatureNo, CategoryNo, CodeNo);
GO

-- 機能ごとの読み込み・差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CodeMaster_FeatureNo_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.CodeMaster'))
CREATE INDEX IX_CodeMaster_FeatureNo_UpdatedAt ON dbo.CodeMaster (FeatureNo, UpdatedAt);
GO

-- 初期値 (作業管理 = 機能番号 2)。無いコードだけを入れる。作成者・更新者は 0 (特定のユーザーではない)
-- 区分 1: 状態 (未対応・作業予定・作業中・完了) / 区分 2: 進捗度 (コード番号 1〜11 が 0%〜100%。70・80・90 だけ名前がある) / 区分 3: 優先度
INSERT INTO dbo.CodeMaster (IsDeleted, CreatedByUserId, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0, 0, 0, 2, v.CategoryNo, v.CodeNo, v.CodeNo, v.NumericValue, v.Name
FROM (VALUES
    (1, 1, 0, N'未対応'), (1, 2, 0, N'作業予定'), (1, 3, 0, N'作業中'), (1, 4, 0, N'完了'),
    (2, 1, 0, N''), (2, 2, 10, N''), (2, 3, 20, N''), (2, 4, 30, N''), (2, 5, 40, N''), (2, 6, 50, N''), (2, 7, 60, N''),
    (2, 8, 70, N'作成完了'), (2, 9, 80, N'内部レビュー完了'), (2, 10, 90, N'外部レビュー完了'), (2, 11, 100, N''),
    (3, 1, 0, N'高'), (3, 2, 0, N'中'), (3, 3, 0, N'低')
) AS v (CategoryNo, CodeNo, NumericValue, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CodeMaster c WHERE c.FeatureNo = 2 AND c.CategoryNo = v.CategoryNo AND c.CodeNo = v.CodeNo);
GO
