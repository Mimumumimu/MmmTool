-- MmmTool の DB (SQL Server)。リマインダーの送信 (MmmBatch)のための、送信先と送信設定の 2 つの表を作る。
-- 設計は docs/specs/database.md。何度流しても壊れない (無いものだけを作る)。001_CreateTables.sql のあとに流す。
-- 使い方: 先に DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する (sqlcmd で流すときは、フィルターつきのインデックスのため、-I を付ける)。
-- 送信の状況の表 (ReminderSendStatus)は、MmmBatch 側の src/Data/MmmBatch.Data.SqlServer/Sql/001_CreateTables.sql が作る。

-- 送信先 (ntfy・Discord)の登録。1 行が 1 つの送信先。Kind は ntfy = 1・Discord = 2。Value は ntfy のトピック名か、Discord の Webhook の URL (秘密)
IF OBJECT_ID(N'dbo.NotificationChannel', N'U') IS NULL
CREATE TABLE dbo.NotificationChannel
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationChannel PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_NotificationChannel_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NotificationChannel_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_NotificationChannel_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NotificationChannel_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_NotificationChannel_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
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

-- 同じリマインダーに、同じ送信先を重ねない (削除されていない行の間だけ一意)。
-- 以前の、リマインダー 1 件につき送信先を 1 つに絞る一意インデックス (UX_ReminderSendSetting_ReminderId)は、複数を選べるようにするため、外す
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ReminderSendSetting_ReminderId' AND object_id = OBJECT_ID(N'dbo.ReminderSendSetting'))
DROP INDEX UX_ReminderSendSetting_ReminderId ON dbo.ReminderSendSetting;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ReminderSendSetting_ReminderId_ChannelId' AND object_id = OBJECT_ID(N'dbo.ReminderSendSetting'))
CREATE UNIQUE INDEX UX_ReminderSendSetting_ReminderId_ChannelId ON dbo.ReminderSendSetting (ReminderId, ChannelId) WHERE IsDeleted = 0;
GO
