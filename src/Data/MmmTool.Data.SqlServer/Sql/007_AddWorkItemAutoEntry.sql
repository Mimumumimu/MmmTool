-- 版 7: 作業に、毎日決まった工数を日ごとの記録として自動で入れる設定の列 AutoHours・AutoWeekdays を足す。Web 版通知の宛先から送信先の行を探すプロシージャ FindWebPushChannel を足す。設計は docs/specs/work-items.md の「自動入力」と docs/specs/database.md と docs/specs/web.md の「通知 (Web 版)」。
-- 何度流しても壊れない。無いものだけを作る。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。SQLite は、同じ列を、あるべき形の schema.sql が足す。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 自動入力の時間。0 は自動入力しない。0 より大きいと、作業管理のページを開いたとき、システムの今日の記録が無ければ、この時間を入れる。ALTER で付いた列は表の末尾にあるので、並びは docs/specs/database.md の WorkItem の表のとおり ReportExclusion の次に直す
IF COL_LENGTH(N'dbo.WorkItem', N'AutoHours') IS NULL
ALTER TABLE dbo.WorkItem ADD AutoHours decimal(4,2) NOT NULL CONSTRAINT DF_WorkItem_AutoHours DEFAULT (0) CONSTRAINT CK_WorkItem_AutoHours CHECK (AutoHours >= 0 AND AutoHours <= 24);
GO

-- 自動入力する曜日。月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数で、Reminder.Weekdays と同じ。0 と 127 は、どちらも毎日。並びは AutoHours の次に直す
IF COL_LENGTH(N'dbo.WorkItem', N'AutoWeekdays') IS NULL
ALTER TABLE dbo.WorkItem ADD AutoWeekdays tinyint NOT NULL CONSTRAINT DF_WorkItem_AutoWeekdays DEFAULT (0) CONSTRAINT CK_WorkItem_AutoWeekdays CHECK (AutoWeekdays BETWEEN 0 AND 127);
GO

-- 自動入力の設定がある作業を、担当者から探す。大半の作業は設定が無いので、設定のある作業だけを入れる。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItem_AssigneeUserId_AutoHours' AND object_id = OBJECT_ID(N'dbo.WorkItem'))
CREATE INDEX IX_WorkItem_AssigneeUserId_AutoHours ON dbo.WorkItem (AssigneeUserId) WHERE IsDeleted = 0 AND AutoHours > 0;
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
