-- MmmBatch の DB (SQL Server)。dbo スキーマに、送信の状況の表 ReminderSendStatus を作る。
-- 設計は docs/specs/database.md。何度流しても壊れない (無いものだけを作る)。
-- 使い方: MmmTool の 001_CreateTables.sql と 002_CreateSendTables.sql を流した DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する。

-- リマインダーの送信の状況。リマインダー × 送信先 × 日ごとに 1 行。ログではなく状態で、「送ったか」の判断に使う。
-- Status は Pending = 1 (送信中)・Sent = 2・Failed = 3。SentAt の 9999-12-31 は、まだ送っていない。MmmBatch はユーザーではないので、作成者・更新者は 0
IF OBJECT_ID(N'dbo.ReminderSendStatus', N'U') IS NULL
CREATE TABLE dbo.ReminderSendStatus
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReminderSendStatus PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_ReminderSendStatus_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderSendStatus_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_ReminderSendStatus_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderSendStatus_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_ReminderSendStatus_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ReminderId      int             NOT NULL,
    ChannelId       int             NOT NULL,
    Date            date            NOT NULL,
    Status          tinyint         NOT NULL CONSTRAINT CK_ReminderSendStatus_Status CHECK (Status BETWEEN 1 AND 3),
    Attempts        tinyint         NOT NULL CONSTRAINT DF_ReminderSendStatus_Attempts DEFAULT (0),
    LastError       nvarchar(500)   NOT NULL CONSTRAINT DF_ReminderSendStatus_LastError DEFAULT (N''),
    SentAt          datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderSendStatus_SentAt DEFAULT ('9999-12-31'),
    CONSTRAINT UQ_ReminderSendStatus_Key UNIQUE (ReminderId, ChannelId, Date)
);
GO
