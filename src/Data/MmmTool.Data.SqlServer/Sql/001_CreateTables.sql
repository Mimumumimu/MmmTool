-- MmmTool の DB です。SQL Server の dbo スキーマに、MmmTool が使う全部の表と索引を作り、コードマスタの初期値を入れる。この 1 本が、1.0.0 の全部の DDL。
-- 設計は docs/specs/database.md と docs/specs/work-items.md。何度流しても壊れない。無いものだけを作り、入れる。DELETE 文は含まない。
-- 内容: ① ユーザー・リマインダー ② 送信先とユーザーごとの設定 ③ リマインダーの送信設定 ④ メッセージ ⑤ 作業管理 ⑥ ノート ⑦ コードマスタと作業管理のコードの初期値 ⑧ ストアドプロシージャ
-- 使い方: 先に DB を作り、その DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する。sqlcmd で流すときは、フィルターつきのインデックスのため、-I を付ける。SSMS は既定でオンになっている。日本語を含むので、ファイルは UTF-8 で、sqlcmd には -f 65001 を付ける。
-- 送信の状況の表 (ReminderSendStatus)は、MmmBatch 側の src/Data/MmmBatch.Data.SqlServer/Sql/001_CreateTables.sql が作る。
-- 1.0.0 より前の更新用スクリプト (002〜013)は、この 1 本にまとめた。1.0.0 のあとの変更は、次の番号 014 から、新しいスクリプトに書く。

-- ユーザー。1 行が 1 人で、ログイン名とパスワードで特定する。PasswordHash の空文字は、管理者が再設定したあとの、新しく決める状態
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

-- ログイン名は、削除されていない行の間だけ一意で、大文字と小文字は区別しない。削除済みのユーザーのログイン名は、新しい人が使える
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUser_LoginName' AND object_id = OBJECT_ID(N'dbo.AppUser'))
CREATE UNIQUE INDEX UX_AppUser_LoginName ON dbo.AppUser (LoginName) WHERE IsDeleted = 0;
GO

-- メッセージの宛先に選べる人を、表示名の順に読む。業務の列だけで作る
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUser_DisplayName' AND object_id = OBJECT_ID(N'dbo.AppUser'))
CREATE INDEX IX_AppUser_DisplayName ON dbo.AppUser (DisplayName);
GO

-- リマインダー。AuthorUserId は作った人で、見える範囲の条件に使う。共通ヘッダーの CreatedByUserId は条件に使わない。TargetUserId は宛先で、0 は全員宛て。人を表す 2 つの列を続けて並べる。Weekdays は月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数で、0 は曜日指定では毎日
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

-- 宛先が自分か全員宛てのリマインダーを探す。見える範囲は、宛先と作成者の 2 つの条件を合わせる。業務の列だけで作る。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_TargetUserId' AND object_id = OBJECT_ID(N'dbo.Reminder'))
CREATE INDEX IX_Reminder_TargetUserId ON dbo.Reminder (TargetUserId);
GO

-- 自分が作ったリマインダーを探す。業務の列だけで作る。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_AuthorUserId' AND object_id = OBJECT_ID(N'dbo.Reminder'))
CREATE INDEX IX_Reminder_AuthorUserId ON dbo.Reminder (AuthorUserId);
GO

-- リマインダーの毎分の読み直しを、差分 (前回より後に更新された行だけ)にする。見える範囲 (宛先が自分か全員宛て、または自分が作成)の絞り込みは、更新日時で絞った行に対して行う。論理削除された行も含めて探す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.Reminder'))
CREATE INDEX IX_Reminder_UpdatedAt ON dbo.Reminder (UpdatedAt);
GO

-- リマインダーの対応状態。人ごと・リマインダーごとに 1 件。Status は 0 = 未対応・1 = 完了・2 = スヌーズ。未対応に戻したときは、行を消さずに Status を 0 にして残す
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
    Status          tinyint         NOT NULL CONSTRAINT CK_ReminderState_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT UQ_ReminderState_ReminderId_UserId UNIQUE (ReminderId, UserId)
);
GO

-- 自分の対応状態。業務の列 UserId で絞り、更新日時の順に探す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReminderState_UserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.ReminderState'))
CREATE INDEX IX_ReminderState_UserId_UpdatedAt ON dbo.ReminderState (UserId, UpdatedAt);
GO

-- 送信先の登録で、種類は ntfy・Discord・Web 版通知。1 行が 1 つの送信先。UserId はこの送信先を使う人で、一覧・選択・更新・削除の条件に使う。共通ヘッダーの CreatedByUserId は条件に使わない。UserId は、区分・名前・値より前に置く。Kind は ntfy = 1・Discord = 2・Web 版通知 = 3。Value は ntfy のトピック名か、Discord の Webhook の URL (秘密)か、Web 版通知の宛先 (JSON)
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
    Kind            tinyint         NOT NULL CONSTRAINT CK_NotificationChannel_Kind CHECK (Kind IN (1, 2, 3)),
    Name            nvarchar(50)    NOT NULL,
    Value           nvarchar(500)   NOT NULL
);
GO

-- 自分の送信先の一覧を、使用者 UserId で引く。業務の列だけで作る。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificationChannel_UserId' AND object_id = OBJECT_ID(N'dbo.NotificationChannel'))
CREATE INDEX IX_NotificationChannel_UserId ON dbo.NotificationChannel (UserId);
GO

-- ユーザーごとの設定。人・機能・区分・コード番号ごとに、値を 1 行で持つ縦持ちの表。値は文字列で、整数は読むときに直す。
-- FeatureNo は、CodeMaster と同じ台帳 CodeFeatures の番号で、どの機能にも属さない共通は 0。メッセージの受け取り先は、共通 0・区分 1・コード 1 で、値は NotificationChannel.Id。外すときは論理削除する
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
    FeatureNo       int             NOT NULL CONSTRAINT CK_AppUserSetting_FeatureNo CHECK (FeatureNo >= 0),
    CategoryNo      int             NOT NULL CONSTRAINT CK_AppUserSetting_CategoryNo CHECK (CategoryNo >= 1),
    CodeNo          int             NOT NULL CONSTRAINT CK_AppUserSetting_CodeNo CHECK (CodeNo >= 1),
    Value           nvarchar(200)   NOT NULL CONSTRAINT DF_AppUserSetting_Value DEFAULT (N'')
);
GO

-- 同じ人・同じ設定が重ならない (削除されていない行の間だけ一意)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUserSetting_UserId_FeatureNo_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.AppUserSetting'))
CREATE UNIQUE INDEX UX_AppUserSetting_UserId_FeatureNo_CategoryNo_CodeNo ON dbo.AppUserSetting (UserId, FeatureNo, CategoryNo, CodeNo) WHERE IsDeleted = 0;
GO

-- 「ノートの通知を受け取る」がオンの人を、人の全件を調べずに、人の番号の順に探す。MmmBatch が、ノートの更新のたびに読む。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUserSetting_FeatureNo_CategoryNo_CodeNo_Value_UserId' AND object_id = OBJECT_ID(N'dbo.AppUserSetting'))
CREATE INDEX IX_AppUserSetting_FeatureNo_CategoryNo_CodeNo_Value_UserId ON dbo.AppUserSetting (FeatureNo, CategoryNo, CodeNo, Value, UserId) WHERE IsDeleted = 0;
GO

-- 管理者がユーザーごとに決める設定。作りは AppUserSetting と同じで、書くのは管理ツール MmmToolAdmin だけ。
-- 区分 1 が機能の利用で、CodeNo は 1、値は 0 = 無効・1 = 有効・2 = 読み取り専用。行が無い人は有効。外すときは論理削除する
IF OBJECT_ID(N'dbo.AppUserAdminSetting', N'U') IS NULL
CREATE TABLE dbo.AppUserAdminSetting
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUserAdminSetting PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_AppUserAdminSetting_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserAdminSetting_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_AppUserAdminSetting_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserAdminSetting_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_AppUserAdminSetting_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    UserId          int             NOT NULL CONSTRAINT CK_AppUserAdminSetting_UserId CHECK (UserId >= 0),
    FeatureNo       int             NOT NULL CONSTRAINT CK_AppUserAdminSetting_FeatureNo CHECK (FeatureNo >= 1),
    CategoryNo      int             NOT NULL CONSTRAINT CK_AppUserAdminSetting_CategoryNo CHECK (CategoryNo >= 1),
    CodeNo          int             NOT NULL CONSTRAINT CK_AppUserAdminSetting_CodeNo CHECK (CodeNo >= 1),
    Value           nvarchar(200)   NOT NULL CONSTRAINT DF_AppUserAdminSetting_Value DEFAULT (N'')
);
GO

-- 同じ人・同じ設定が重ならない (削除されていない行の間だけ一意)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUserAdminSetting_UserId_FeatureNo_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.AppUserAdminSetting'))
CREATE UNIQUE INDEX UX_AppUserAdminSetting_UserId_FeatureNo_CategoryNo_CodeNo ON dbo.AppUserAdminSetting (UserId, FeatureNo, CategoryNo, CodeNo) WHERE IsDeleted = 0;
GO

-- リマインダーの送信設定。どのリマインダーを、どの送信先へ送るか。1 つのリマインダーに、送信先を複数選べて、1 つの送信先につき 1 行になる。選ばなくなった送信先の行は、論理削除する
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

-- 会話に入っている人 1 人 1 行。グループを抜けるときは論理削除する。
-- LastReadMessageId は、その人が読んだ最後のメッセージの Id。0 は何も読んでいない。大きくなるときだけ書く
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
    UserId          int             NOT NULL CONSTRAINT CK_MessageConversationMember_UserId CHECK (UserId >= 0),
    LastReadMessageId int           NOT NULL CONSTRAINT DF_MessageConversationMember_LastReadMessageId DEFAULT (0) CONSTRAINT CK_MessageConversationMember_LastReadMessageId CHECK (LastReadMessageId >= 0)
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

-- 自分が新しく入った会話と、読んだ位置が変わった会話を、差分で知る (削除された行も含める)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageConversationMember_UserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.MessageConversationMember'))
CREATE INDEX IX_MessageConversationMember_UserId_UpdatedAt ON dbo.MessageConversationMember (UserId, UpdatedAt);
GO

-- Web 版のサーバーが、更新された参加者の行 (読んだ位置の変わり)を、人を特定せずに、全員分まとめて調べる。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageConversationMember_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.MessageConversationMember'))
CREATE INDEX IX_MessageConversationMember_UpdatedAt ON dbo.MessageConversationMember (UpdatedAt);
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
-- Status は、送信待ちと送信中が 1、送信済みが 2、失敗が 3、送らなかったが 4 で、通知を受ける人が管理者にメッセージを無効にされたときに付く。SentAt の 9999-12-31 は、まだ送っていない。失敗の理由は持たず、MmmBatch のログに残す。
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
    Status          tinyint         NOT NULL CONSTRAINT DF_MessageDelivery_Status DEFAULT (1) CONSTRAINT CK_MessageDelivery_Status CHECK (Status BETWEEN 1 AND 4),
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
-- AssigneeUserId は担当者で、AppUser.Id を入れる。0 は未割り当て
-- Link は、案件・グループ・作業に付ける 1 つのリンクで、URL・フォルダ・ファイルのパスのどれか。無いときは空文字
-- ReportExclusion は、作業報告のどの部分から外すか。0 は外さない、1 は予定から外して実績には出す、2 は実績と予定の両方から外す。定例の会議などに付ける
-- AutoHours は自動入力の時間。0 は自動入力しない。0 より大きいと、作業管理のページを開いたとき、システムの今日の記録が無ければ、この時間を入れる
-- AutoWeekdays は自動入力する曜日。月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数で、Reminder.Weekdays と同じ。0 と 127 は、どちらも毎日
-- PriorityCodeNo・StatusCodeNo・ProgressCodeNo は、下のコードマスタ CodeMaster のコード番号で、区分は 3・1・2。外部キーは使わない
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
    ReportExclusion  tinyint         NOT NULL CONSTRAINT DF_WorkItem_ReportExclusion DEFAULT (0) CONSTRAINT CK_WorkItem_ReportExclusion CHECK (ReportExclusion IN (0, 1, 2)),
    AutoHours        decimal(4, 2)   NOT NULL CONSTRAINT DF_WorkItem_AutoHours DEFAULT (0) CONSTRAINT CK_WorkItem_AutoHours CHECK (AutoHours >= 0 AND AutoHours <= 24),
    AutoWeekdays     tinyint         NOT NULL CONSTRAINT DF_WorkItem_AutoWeekdays DEFAULT (0) CONSTRAINT CK_WorkItem_AutoWeekdays CHECK (AutoWeekdays BETWEEN 0 AND 127),
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

-- 作業報告の実績と予定で、担当が自分の作業を探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_AssigneeUserId_StatusCodeNo' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_AssigneeUserId_StatusCodeNo ON dbo.WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;
GO

-- 予定 (状態が作業予定・作業中)と、期間内に完了した作業の担当者を、表を読まずに探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_StatusCodeNo_CompletedDate_AssigneeUserId ON dbo.WorkItem (StatusCodeNo, CompletedDate, AssigneeUserId) WHERE IsDeleted = 0;
GO

-- 自動入力の設定がある作業を、担当者から探す。大半の作業は設定が無いので、設定のある作業だけを入れる。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_AssigneeUserId_AutoHours' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_AssigneeUserId_AutoHours ON dbo.WorkItem (AssigneeUserId) WHERE IsDeleted = 0 AND AutoHours > 0;
GO

-- 日ごとの記録。作業ごと・日ごとに 1 件。ProjectId は作業の案件で、選んだ案件に絞って読むために持つ。Id は bigint
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
    Hours           decimal(4,2)    NOT NULL CONSTRAINT DF_WorkRecord_Hours DEFAULT (0) CONSTRAINT CK_WorkRecord_Hours CHECK (Hours >= 0 AND Hours <= 24),
    Note            nvarchar(1000)  NOT NULL CONSTRAINT DF_WorkRecord_Note DEFAULT (N'')
);
GO

-- 作業ごと・日ごとに 1 件。人は区別しない。同じ日に入れ直したときは、この行を使い回す。論理削除された行も含めて一意。作業の累計の計算し直しにも使う
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

-- 期間内に記録がある作業の番号を、日付から探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkRecord_Date_WorkItemId' AND object_id = OBJECT_ID(N'dbo.WorkRecord'))
CREATE INDEX IX_WorkRecord_Date_WorkItemId ON dbo.WorkRecord (Date, WorkItemId) WHERE IsDeleted = 0;
GO

-- 日報メモ。作業に結びつかない、その人の 1 日分の記入で、打ち合わせなどを書く。1 人 1 日 1 件。持ち主は業務の列 UserId で持ち、CreatedByUserId は条件に使わない
-- 空にしたときは、行を消さず Note を空文字にして、論理削除しない。IsDeleted は共通ヘッダーの列で、この表の操作では使わない
IF OBJECT_ID(N'dbo.WorkDayNote', N'U') IS NULL
CREATE TABLE dbo.WorkDayNote
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkDayNote PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_WorkDayNote_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkDayNote_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_WorkDayNote_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_WorkDayNote_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_WorkDayNote_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    UserId          int             NOT NULL CONSTRAINT CK_WorkDayNote_UserId CHECK (UserId >= 1),
    Date            date            NOT NULL,
    Note            nvarchar(2000)  NOT NULL CONSTRAINT DF_WorkDayNote_Note DEFAULT (N'')
);
GO

-- 1 人 1 日 1 件。同じ日に書き直したときは、この行を使い回す。日付の範囲の読み込みと、直近の日の検索にも使う
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkDayNote_UserId_Date' AND object_id = OBJECT_ID(N'dbo.WorkDayNote'))
CREATE UNIQUE INDEX UX_WorkDayNote_UserId_Date ON dbo.WorkDayNote (UserId, Date);
GO

-- 報告日に日報メモがある人を、日付から探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkDayNote_Date_UserId' AND object_id = OBJECT_ID(N'dbo.WorkDayNote'))
CREATE INDEX IX_WorkDayNote_Date_UserId ON dbo.WorkDayNote (Date, UserId) WHERE IsDeleted = 0;
GO

-- ノート。開始日から終了日までの期間・タイトル・本文・重要度・公開範囲を持つ。書いた人は業務の列 AuthorUserId で持ち、CreatedByUserId は条件に使わない。
-- 終了日が無い (無期限)ときは 9999-12-31。本文は上限を決めない (nvarchar(max))
-- EventDate は実施日。この日に、ノートのカードを目立たせ、起動時の通知ダイアログに出す。未設定は 9999-12-31。設定があるときは、開始日から終了日の間にある
IF OBJECT_ID(N'dbo.Note', N'U') IS NULL
CREATE TABLE dbo.Note
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_Note PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_Note_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Note_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_Note_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Note_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_Note_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    AuthorUserId    int             NOT NULL CONSTRAINT CK_Note_AuthorUserId CHECK (AuthorUserId >= 1),
    IsPrivate       bit             NOT NULL CONSTRAINT DF_Note_IsPrivate DEFAULT (0),
    StartDate       date            NOT NULL,
    EndDate         date            NOT NULL CONSTRAINT DF_Note_EndDate DEFAULT ('9999-12-31'),
    EventDate       date            NOT NULL CONSTRAINT DF_Note_EventDate DEFAULT ('9999-12-31'),
    Priority        tinyint         NOT NULL CONSTRAINT DF_Note_Priority DEFAULT (3) CONSTRAINT CK_Note_Priority CHECK (Priority BETWEEN 1 AND 5),
    Title           nvarchar(100)   NOT NULL CONSTRAINT CK_Note_Title CHECK (LEN(Title) >= 1),
    Body            nvarchar(max)   NOT NULL CONSTRAINT DF_Note_Body DEFAULT (N''),
    CONSTRAINT CK_Note_Period CHECK (EndDate >= StartDate),
    CONSTRAINT CK_Note_EventDate CHECK (EventDate = '9999-12-31' OR (EventDate >= StartDate AND EventDate <= EndDate))
);
GO

-- 表示する日に有効な行 (EndDate >= その日 かつ StartDate <= その日) を探す。終了日を先頭にして、過去に終わった大量のノートを走査しない。削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Note_EndDate_StartDate' AND object_id = OBJECT_ID(N'dbo.Note'))
CREATE INDEX IX_Note_EndDate_StartDate ON dbo.Note (EndDate, StartDate);
GO

-- 実際の今日が実施日の行を探す。未設定の大量の行 (9999-12-31) を、起動のたびに走査しない。削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Note_EventDate' AND object_id = OBJECT_ID(N'dbo.Note'))
CREATE INDEX IX_Note_EventDate ON dbo.Note (EventDate);
GO

-- 前回読んだ時刻の後に更新されたノートを、全件を調べずに探す。MmmBatch が 1 秒おきに、PC 版が 10 秒おきに読む。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Note_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.Note'))
CREATE INDEX IX_Note_UpdatedAt ON dbo.Note (UpdatedAt);
GO

-- コードマスタ。(FeatureNo, CategoryNo, CodeNo) で 1 件。FeatureNo は機能番号で、台帳は MmmTool.CodeMaster.Core の CodeFeatures。作業管理は 1
-- CategoryNo は機能の中の区分で、コードの種類。作業管理は 1 = 状態・2 = 進捗度・3 = 優先度。CodeNo は区分の中の識別子で、一度使った番号は、同じ意味のまま変えない
-- NumericValue は計算に使う数で、進捗度の % など。要らないコードは 0。Name の「ない」は空文字
-- 全員に影響するので、画面からは編集しない。この表を SQL で直す。選択肢を外すときは IsDeleted = 1 にして、DELETE はしない
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

-- 初期値は、作業管理の機能番号 1 で入れる。無いコードだけを入れる。作成者と更新者は 0 で、特定のユーザーではない
-- 区分 1: 状態で、未対応・作業予定・作業中・保留・完了 / 区分 2: 進捗度で、コード番号 1〜11 が 0%〜100%、名前はすべて空 / 区分 3: 優先度
-- 状態の表示順は、作業中の次が保留で、その次が完了。保留はコード番号 5、完了はコード番号 4 なので、コード番号の順とは違う。「完了」を見分ける処理がコード番号 4 に頼るので、コード番号は変えない
INSERT INTO dbo.CodeMaster (IsDeleted, CreatedByUserId, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0, 0, 0, 1, v.CategoryNo, v.CodeNo, v.SortOrder, v.NumericValue, v.Name
FROM (VALUES
    (1, 1, 1, 0, N'未対応'), (1, 2, 2, 0, N'作業予定'), (1, 3, 3, 0, N'作業中'), (1, 5, 4, 0, N'保留'), (1, 4, 5, 0, N'完了'),
    (2, 1, 1, 0, N''), (2, 2, 2, 10, N''), (2, 3, 3, 20, N''), (2, 4, 4, 30, N''), (2, 5, 5, 40, N''), (2, 6, 6, 50, N''), (2, 7, 7, 60, N''),
    (2, 8, 8, 70, N''), (2, 9, 9, 80, N''), (2, 10, 10, 90, N''), (2, 11, 11, 100, N''),
    (3, 1, 1, 0, N'高'), (3, 2, 2, 0, N'中'), (3, 3, 3, 0, N'低')
) AS v (CategoryNo, CodeNo, SortOrder, NumericValue, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CodeMaster c WHERE c.FeatureNo = 1 AND c.CategoryNo = v.CategoryNo AND c.CodeNo = v.CodeNo);
GO

-- Web 版通知の宛先 (endpoint)から、自分の送信先の行を探す。Web 版の DB のログイン MmmToolWeb は、秘密の列 Value を読めないので、表の持ち主 (dbo)の権限で動くこのプロシージャに探させる。
-- 返すのは Id と IsDeleted だけで、Value は返さない。複数あるときは、削除されていない行を先にして、番号の大きい方。比べるのは endpoint だけで、鍵が替わっていても見つかる。
-- 端末が登録の番号の記憶を失っても、登録済みかを正しく出し、登録し直したときに行を増やさないために、Web 版が呼ぶ。
-- 実行の権限は、このファイルでは付けない。docs/specs/web.md の「DB の接続」の権限の手順の GRANT EXECUTE を流す
CREATE OR ALTER PROCEDURE dbo.FindWebPushChannel
    @UserId   int,
    @Endpoint nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) Id, IsDeleted
    FROM dbo.NotificationChannel
    WHERE UserId = @UserId AND Kind = 3
      AND JSON_VALUE(Value, N'$.endpoint') = @Endpoint
    ORDER BY IsDeleted, Id DESC;
END
GO
