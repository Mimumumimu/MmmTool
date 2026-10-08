-- MmmTool の DB (SQL Server)。dbo スキーマに、AppUser / Reminder / ReminderState の 3 つの表を作る。
-- 設計は docs/specs/database.md。何度流しても壊れない (無いものだけを作る)。
-- 使い方: 先に DB を作り、その DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する。

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

-- リマインダー。宛先 TargetUserId の 0 は全員宛て。Weekdays は月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数 (0 は曜日指定では毎日)
IF OBJECT_ID(N'dbo.Reminder', N'U') IS NULL
CREATE TABLE dbo.Reminder
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reminder PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_Reminder_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Reminder_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_Reminder_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Reminder_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_Reminder_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
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
