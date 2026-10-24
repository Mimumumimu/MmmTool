-- 版 6: 送信先 NotificationChannel の区分 Kind に、Web 版通知 = 3 を足す。メッセージを会話の形に作り直し、古い MessageSendRequest を削除して、MessageConversation・MessageConversationMember・Message・MessageDelivery と、ユーザーごとの設定 AppUserSetting を作る。設計は docs/specs/database.md と docs/specs/messages.md。
-- 何度流しても壊れない。値 3 を許す検査が既にあるときは、何もしない。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。古い MessageSendRequest は、表ごと削除するので、既存のメッセージの送信依頼は消える。
-- SQLite には送信先の表もユーザーの表も無いので、この変更は SQL Server だけ。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 区分の値の範囲を (1, 2)から (1, 2, 3)に広げる。検査は定義を変えられないので、付け替える。付け替える間に新しい行が入らないよう、同じバッチで続けて行う
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_NotificationChannel_Kind' AND parent_object_id = OBJECT_ID(N'dbo.NotificationChannel') AND definition NOT LIKE N'%(3)%')
BEGIN
    ALTER TABLE dbo.NotificationChannel DROP CONSTRAINT CK_NotificationChannel_Kind;
    ALTER TABLE dbo.NotificationChannel ADD CONSTRAINT CK_NotificationChannel_Kind CHECK (Kind IN (1, 2, 3));
END
GO

-- メッセージを会話の形に作り直すため、古いメッセージの送信依頼の表を削除する。タイトルと送信先 (ChannelId)を持つ形は、会話の形では使わない
IF OBJECT_ID(N'dbo.MessageSendRequest', N'U') IS NOT NULL
DROP TABLE dbo.MessageSendRequest;
GO

-- メッセージの宛先に選べる人を、表示名の順に読む。業務の列だけで作る
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUser_DisplayName' AND object_id = OBJECT_ID(N'dbo.AppUser'))
CREATE INDEX IX_AppUser_DisplayName ON dbo.AppUser (DisplayName);
GO

-- ユーザーごとの設定。人・機能・区分・コード番号ごとに、値を 1 行で持つ縦持ちの表。値は文字列で、整数は読むときに直す。
-- FeatureNo は、CodeMaster と同じ台帳 CodeFeatures の番号。メッセージは 3 で、区分 1 が受け取り先で、値は NotificationChannel.Id。外すときは論理削除する
IF OBJECT_ID(N'dbo.AppUserSetting', N'U') IS NULL
CREATE TABLE dbo.AppUserSetting
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUserSetting PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_AppUserSetting_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserSetting_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_AppUserSetting_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserSetting_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_AppUserSetting_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    UserId          int             NOT NULL CONSTRAINT CK_AppUserSetting_UserId CHECK (UserId >= 0),
    FeatureNo       int             NOT NULL CONSTRAINT CK_AppUserSetting_FeatureNo CHECK (FeatureNo >= 1),
    CategoryNo      int             NOT NULL CONSTRAINT CK_AppUserSetting_CategoryNo CHECK (CategoryNo >= 1),
    CodeNo          int             NOT NULL CONSTRAINT CK_AppUserSetting_CodeNo CHECK (CodeNo >= 1),
    Value           nvarchar(200)   NOT NULL CONSTRAINT DF_AppUserSetting_Value DEFAULT (N'')
);
GO

-- 同じ人・同じ設定が重ならない (削除されていない行の間だけ一意)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUserSetting_UserId_FeatureNo_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.AppUserSetting'))
CREATE UNIQUE INDEX UX_AppUserSetting_UserId_FeatureNo_CategoryNo_CodeNo ON dbo.AppUserSetting (UserId, FeatureNo, CategoryNo, CodeNo) WHERE IsDeleted = 0;
GO

-- メッセージの会話。Kind は 1 対 1 = 1・グループ = 2。Name はグループの名前で、1 対 1 は空文字。
-- 1 対 1 の重なりは、索引ではなく、会話を作る処理が鍵 (sp_getapplock)で防ぐ。最後のメッセージの番号は持たない
IF OBJECT_ID(N'dbo.MessageConversation', N'U') IS NULL
CREATE TABLE dbo.MessageConversation
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_MessageConversation PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_MessageConversation_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageConversation_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_MessageConversation_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageConversation_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_MessageConversation_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    Kind            tinyint         NOT NULL CONSTRAINT CK_MessageConversation_Kind CHECK (Kind BETWEEN 1 AND 2),
    Name            nvarchar(50)    NOT NULL CONSTRAINT DF_MessageConversation_Name DEFAULT (N'')
);
GO

-- 会話に入っている人 1 人 1 行。グループを抜けるときは論理削除する
IF OBJECT_ID(N'dbo.MessageConversationMember', N'U') IS NULL
CREATE TABLE dbo.MessageConversationMember
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_MessageConversationMember PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_MessageConversationMember_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageConversationMember_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_MessageConversationMember_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageConversationMember_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_MessageConversationMember_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ConversationId  int             NOT NULL CONSTRAINT CK_MessageConversationMember_ConversationId CHECK (ConversationId >= 1),
    UserId          int             NOT NULL CONSTRAINT CK_MessageConversationMember_UserId CHECK (UserId >= 0)
);
GO

-- 同じ会話に、同じ人が二重に入らない (削除されていない行の間だけ一意)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MessageConversationMember_ConversationId_UserId' AND object_id = OBJECT_ID(N'dbo.MessageConversationMember'))
CREATE UNIQUE INDEX UX_MessageConversationMember_ConversationId_UserId ON dbo.MessageConversationMember (ConversationId, UserId) WHERE IsDeleted = 0;
GO

-- 自分の会話の一覧と、2 人の 1 対 1 の会話を探す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageConversationMember_UserId_ConversationId' AND object_id = OBJECT_ID(N'dbo.MessageConversationMember'))
CREATE INDEX IX_MessageConversationMember_UserId_ConversationId ON dbo.MessageConversationMember (UserId, ConversationId);
GO

-- 自分が新しく入った会話を、差分で知る (削除された行も含める)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageConversationMember_UserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.MessageConversationMember'))
CREATE INDEX IX_MessageConversationMember_UserId_UpdatedAt ON dbo.MessageConversationMember (UserId, UpdatedAt);
GO

-- 会話の中の 1 通。返信でなければ ReplyToMessageId は 0。タイトルと送る先は持たない。送る先は、MmmBatch が送るときに、受け取り先から読む。
-- UpdatedAt は、通知の状態が変わるたびに、MmmBatch が同じトランザクションで更新する (端末の差分の取得の基準)
IF OBJECT_ID(N'dbo.Message', N'U') IS NULL
CREATE TABLE dbo.Message
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_Message PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_Message_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Message_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_Message_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Message_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_Message_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    ConversationId  int             NOT NULL CONSTRAINT CK_Message_ConversationId CHECK (ConversationId >= 1),
    SenderUserId    int             NOT NULL CONSTRAINT CK_Message_SenderUserId CHECK (SenderUserId >= 0),
    ReplyToMessageId int            NOT NULL CONSTRAINT DF_Message_ReplyToMessageId DEFAULT (0) CONSTRAINT CK_Message_ReplyToMessageId CHECK (ReplyToMessageId >= 0),
    Body            nvarchar(1000)  NOT NULL
);
GO

-- 会話の 50 件を Id で区切って読む。人の一覧で、会話ごとの最新の 1 通を読むのにも使う
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Message_ConversationId_Id' AND object_id = OBJECT_ID(N'dbo.Message'))
CREATE INDEX IX_Message_ConversationId_Id ON dbo.Message (ConversationId, Id DESC);
GO

-- 自分が送ったメッセージの変わり (通知の状態の印)を、差分で読む
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Message_SenderUserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.Message'))
CREATE INDEX IX_Message_SenderUserId_UpdatedAt ON dbo.Message (SenderUserId, UpdatedAt);
GO

-- Web 版のサーバーが、数秒おきに、更新された会話の番号だけを調べる
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Message_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.Message'))
CREATE INDEX IX_Message_UpdatedAt ON dbo.Message (UpdatedAt);
GO

-- 1 通を、1 人に通知する状態。送った人以外の参加者 1 人が 1 行で、自分との会話は自分に 1 行。MmmBatch が送って状態を更新する。
-- Status は、送信待ちと送信中が 1、送信済みが 2、失敗が 3。SentAt の 9999-12-31 は、まだ送っていない。失敗の理由は持たず、MmmBatch のログに残す。
-- 作成者・更新者は、頼んだときは送った人、MmmBatch が更新するときの更新者は 0
IF OBJECT_ID(N'dbo.MessageDelivery', N'U') IS NULL
CREATE TABLE dbo.MessageDelivery
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_MessageDelivery PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_MessageDelivery_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageDelivery_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_MessageDelivery_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageDelivery_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_MessageDelivery_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    MessageId       int             NOT NULL CONSTRAINT CK_MessageDelivery_MessageId CHECK (MessageId >= 1),
    UserId          int             NOT NULL CONSTRAINT CK_MessageDelivery_UserId CHECK (UserId >= 0),
    Status          tinyint         NOT NULL CONSTRAINT DF_MessageDelivery_Status DEFAULT (1) CONSTRAINT CK_MessageDelivery_Status CHECK (Status BETWEEN 1 AND 3),
    Attempts        tinyint         NOT NULL CONSTRAINT DF_MessageDelivery_Attempts DEFAULT (0),
    SentAt          datetimeoffset(3) NOT NULL CONSTRAINT DF_MessageDelivery_SentAt DEFAULT ('9999-12-31')
);
GO

-- 同じ人に、同じメッセージの通知が二重に書かれない
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MessageDelivery_MessageId_UserId' AND object_id = OBJECT_ID(N'dbo.MessageDelivery'))
CREATE UNIQUE INDEX UX_MessageDelivery_MessageId_UserId ON dbo.MessageDelivery (MessageId, UserId);
GO

-- MmmBatch が 1 秒おきに読む送信待ち・失敗の行を、状態で探す (送信済みの大量の行を、読むたびに走査しないため)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageDelivery_Status' AND object_id = OBJECT_ID(N'dbo.MessageDelivery'))
CREATE INDEX IX_MessageDelivery_Status ON dbo.MessageDelivery (Status);
GO

-- 自分宛のメッセージの新着と、通知の状態の変わりを、会話の数に関係なく、索引 1 回で差分に読む
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageDelivery_UserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.MessageDelivery'))
CREATE INDEX IX_MessageDelivery_UserId_UpdatedAt ON dbo.MessageDelivery (UserId, UpdatedAt);
GO
