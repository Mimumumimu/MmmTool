-- 版 3: ノートの表 (dbo.Note) を足す。設計は docs/specs/notes.md と docs/specs/database.md。
-- 何度流しても壊れない (無いものだけを作る)。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- ノート。開始日から終了日までの期間・タイトル・本文・重要度・公開範囲を持つ。書いた人は業務の列 AuthorUserId で持つ (CreatedByUserId は条件に使わない)。
-- 終了日が無い (無期限)ときは 9999-12-31。本文は上限を決めない (nvarchar(max))
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
    Priority        tinyint         NOT NULL CONSTRAINT DF_Note_Priority DEFAULT (3) CONSTRAINT CK_Note_Priority CHECK (Priority BETWEEN 1 AND 5),
    Title           nvarchar(100)   NOT NULL CONSTRAINT CK_Note_Title CHECK (LEN(Title) >= 1),
    Body            nvarchar(max)   NOT NULL CONSTRAINT DF_Note_Body DEFAULT (N''),
    CONSTRAINT CK_Note_Period CHECK (EndDate >= StartDate)
);
GO

-- 表示する日に有効な行 (EndDate >= その日 かつ StartDate <= その日) を探す。終了日を先頭にして、過去に終わった大量のノートを走査しない。削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Note_EndDate_StartDate' AND object_id = OBJECT_ID(N'dbo.Note'))
CREATE INDEX IX_Note_EndDate_StartDate ON dbo.Note (EndDate, StartDate);
GO
