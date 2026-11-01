-- MmmBatch の DB です。SQL Server の dbo スキーマに、送信の状況の表 ReminderSendStatus と、その索引を作る。この 1 本が、1.0.0 の全部の DDL。
-- 設計は docs/specs/database.md と docs/specs/web.md。何度流しても壊れない。無いものだけを作る。DELETE 文は含まない。
-- 使い方: MmmTool の 001_CreateTables.sql を流した DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する。
-- 1.0.0 より前の更新用スクリプト 002 は、この 1 本にまとめた。1.0.0 のあとの変更は、次の番号 003 から、新しいスクリプトに書く。

-- リマインダーの送信の状況。リマインダー × 送信先 × 日ごとに 1 行。ログではなく状態で、「送ったか」の判断に使う。
-- Status は Pending = 1 で送信中、Sent = 2、Failed = 3。SentAt の 9999-12-31 は、まだ送っていない。MmmBatch はユーザーではないので、作成者と更新者は 0
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
    SentAt          datetimeoffset(3) NOT NULL CONSTRAINT DF_ReminderSendStatus_SentAt DEFAULT ('9999-12-31'),
    CONSTRAINT UQ_ReminderSendStatus_Key UNIQUE (ReminderId, ChannelId, Date)
);
GO

-- Web 版の「受け取った通知」の一覧が、自分の送信先に届いたリマインダーを、送った日時の新しい順に引く。表は掃除されず増え続けるので、索引で絞る。業務の列だけで作る
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReminderSendStatus_ChannelId_SentAt' AND object_id = OBJECT_ID(N'dbo.ReminderSendStatus'))
CREATE INDEX IX_ReminderSendStatus_ChannelId_SentAt ON dbo.ReminderSendStatus (ChannelId, SentAt DESC);
GO
