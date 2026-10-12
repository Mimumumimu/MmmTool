-- MmmTool の DB (SQL Server)。共通ヘッダーの列 (CreatedByUserId)を条件に使わないよう、業務の列を足す。
-- 設計は docs/specs/database.md の「共通ヘッダーの列を条件に使っている処理の直し」。
-- やること: ① dbo.Reminder に AuthorUserId (作成者。作った人)を足し、今ある行に、作成者の列と同じ値を入れる
--           ② dbo.NotificationChannel に UserId (使用者。この送信先を使う人)を足し、今ある行に、作成者の列と同じ値を入れる
--           ③ 条件に使う業務の列に、索引を足す (Reminder の TargetUserId・AuthorUserId、NotificationChannel の UserId)
-- 何度流しても壊れない (列が無いときだけ足して、値を入れる。索引は、無いときだけ作る)。DELETE 文は含まない。
-- 外部キーは使わない。NULL は使わない。足した列は、ALTER で末尾に付くので、すでにある DB の列の並びは、001・002 の CREATE TABLE (設計した並び)と違う (動作は同じ。並びは SSMS で直す)。
-- 値の移行は、更新日時・更新者を変えない (ユーザーの編集ではないため)。

SET QUOTED_IDENTIFIER ON;
GO

-- ① Reminder.AuthorUserId
IF COL_LENGTH(N'dbo.Reminder', N'AuthorUserId') IS NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    ALTER TABLE dbo.Reminder ADD AuthorUserId int NOT NULL
        CONSTRAINT DF_Reminder_AuthorUserId DEFAULT (0)
        CONSTRAINT CK_Reminder_AuthorUserId CHECK (AuthorUserId >= 0);

    -- 同じバッチの中で、足したばかりの列を使えないので、別のバッチとして実行する
    EXEC (N'UPDATE dbo.Reminder SET AuthorUserId = CreatedByUserId');

    COMMIT TRANSACTION;
END
GO

-- ② NotificationChannel.UserId
IF COL_LENGTH(N'dbo.NotificationChannel', N'UserId') IS NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    ALTER TABLE dbo.NotificationChannel ADD UserId int NOT NULL
        CONSTRAINT DF_NotificationChannel_UserId DEFAULT (0)
        CONSTRAINT CK_NotificationChannel_UserId CHECK (UserId >= 0);

    EXEC (N'UPDATE dbo.NotificationChannel SET UserId = CreatedByUserId');

    COMMIT TRANSACTION;
END
GO

-- ③ 索引。見える範囲 (宛先が自分・全員宛て、または自分が作成)の 2 つの条件と、自分の送信先の一覧
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_TargetUserId' AND object_id = OBJECT_ID(N'dbo.Reminder'))
    CREATE INDEX IX_Reminder_TargetUserId ON dbo.Reminder (TargetUserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_AuthorUserId' AND object_id = OBJECT_ID(N'dbo.Reminder'))
    CREATE INDEX IX_Reminder_AuthorUserId ON dbo.Reminder (AuthorUserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificationChannel_UserId' AND object_id = OBJECT_ID(N'dbo.NotificationChannel'))
    CREATE INDEX IX_NotificationChannel_UserId ON dbo.NotificationChannel (UserId);
GO
