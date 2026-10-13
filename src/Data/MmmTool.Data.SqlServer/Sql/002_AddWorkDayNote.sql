-- 版 2: 日報メモの表 (dbo.WorkDayNote) を足す。設計は docs/specs/work-items.md の「日報メモ」と docs/specs/database.md。
-- 何度流しても壊れない (無いものだけを作る)。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 日報メモ。作業に結びつかない、その人の 1 日分の記入 (打ち合わせなど)。1 人 1 日 1 件。持ち主は業務の列 UserId で持つ (CreatedByUserId は条件に使わない)
-- 空にしたときは、行を消さず Note を空文字にする (論理削除しない)。IsDeleted は共通ヘッダーの列で、この表の操作では使わない
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

-- 1 人 1 日 1 件 (同じ日に書き直したときは、この行を使い回す。日付の範囲の読み込み・直近の日の検索にも使う)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkDayNote_UserId_Date' AND object_id = OBJECT_ID(N'dbo.WorkDayNote'))
CREATE UNIQUE INDEX UX_WorkDayNote_UserId_Date ON dbo.WorkDayNote (UserId, Date);
GO
