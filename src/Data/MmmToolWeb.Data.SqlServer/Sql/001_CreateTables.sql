-- MmmToolWeb の DB (SQL Server)。dbo スキーマに、パスキーの表 AppUserPasskey と、Web 版の認証の状態の表 AppUserWebAuth を作る。
-- 設計は docs/specs/web.md。何度流しても壊れない (無いものだけを作る)。
-- 使い方: MmmTool の 001_CreateTables.sql を流した DB を選んでから、表を作れる権限のあるユーザーで、全体を実行する。

-- パスキー。1 人が複数持てる。CredentialId は、削除されていない行の間だけ一意。SignCount は 32 ビットの符号なしを入れるので bigint
IF OBJECT_ID(N'dbo.AppUserPasskey', N'U') IS NULL
CREATE TABLE dbo.AppUserPasskey
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUserPasskey PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_AppUserPasskey_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserPasskey_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_AppUserPasskey_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserPasskey_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_AppUserPasskey_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    AppUserId       int             NOT NULL,
    CredentialId    varbinary(1024) NOT NULL,
    PublicKey       varbinary(1024) NOT NULL,
    SignCount       bigint          NOT NULL CONSTRAINT DF_AppUserPasskey_SignCount DEFAULT (0),
    UserHandle      varbinary(64)   NOT NULL,
    DeviceName      nvarchar(50)    NOT NULL CONSTRAINT DF_AppUserPasskey_DeviceName DEFAULT (N''),
    LastUsedAt      datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserPasskey_LastUsedAt DEFAULT (SYSDATETIMEOFFSET())
);
GO

-- ログインのとき、パスキーの識別番号から探す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUserPasskey_CredentialId' AND object_id = OBJECT_ID(N'dbo.AppUserPasskey'))
CREATE UNIQUE INDEX UX_AppUserPasskey_CredentialId ON dbo.AppUserPasskey (CredentialId) WHERE IsDeleted = 0;
GO

-- 人ごとのパスキーの一覧
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUserPasskey_AppUserId' AND object_id = OBJECT_ID(N'dbo.AppUserPasskey'))
CREATE INDEX IX_AppUserPasskey_AppUserId ON dbo.AppUserPasskey (AppUserId) WHERE IsDeleted = 0;
GO

-- Web 版の認証の状態。1 人に 1 行 (行が無い人は、登録用コードが無く、SessionVersion は 1 として扱う)。管理者が最初にコードを出すときに行を作る
-- EnrollmentCodeHash は登録用コードの SHA-256 (16 進)。空文字は有効なコードが無い状態。EnrollmentCodeExpiresAt の 9999-12-31 は、コードが無い
IF OBJECT_ID(N'dbo.AppUserWebAuth', N'U') IS NULL
CREATE TABLE dbo.AppUserWebAuth
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUserWebAuth PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_AppUserWebAuth_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserWebAuth_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_AppUserWebAuth_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserWebAuth_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_AppUserWebAuth_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    AppUserId       int             NOT NULL,
    EnrollmentCodeHash varchar(64)  NOT NULL CONSTRAINT DF_AppUserWebAuth_EnrollmentCodeHash DEFAULT (''),
    EnrollmentCodeExpiresAt datetimeoffset(3) NOT NULL CONSTRAINT DF_AppUserWebAuth_EnrollmentCodeExpiresAt DEFAULT ('9999-12-31'),
    SessionVersion  int             NOT NULL CONSTRAINT DF_AppUserWebAuth_SessionVersion DEFAULT (1)
);
GO

-- 1 人に 1 行 (削除されていない行の間だけ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppUserWebAuth_AppUserId' AND object_id = OBJECT_ID(N'dbo.AppUserWebAuth'))
CREATE UNIQUE INDEX UX_AppUserWebAuth_AppUserId ON dbo.AppUserWebAuth (AppUserId) WHERE IsDeleted = 0;
GO

-- 登録用コードから人を探す (コードのハッシュが空の行は探さない)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUserWebAuth_EnrollmentCodeHash' AND object_id = OBJECT_ID(N'dbo.AppUserWebAuth'))
CREATE INDEX IX_AppUserWebAuth_EnrollmentCodeHash ON dbo.AppUserWebAuth (EnrollmentCodeHash) WHERE IsDeleted = 0 AND EnrollmentCodeHash <> '';
GO
