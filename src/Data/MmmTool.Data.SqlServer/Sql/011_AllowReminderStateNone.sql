-- 版 11: リマインダーの対応状態 ReminderState の Status に、未対応 = 0 を許す。未対応に戻したとき、行を消さずに、Status を 0 にして残すため。設計は docs/specs/database.md の「ReminderState」。
-- 何度流しても壊れない。0 を許す検査が既にあるときは、何もしない。DELETE 文は含まない。既存の行の値は変わらない。
-- SQLite には ReminderState の表が無い (ローカルモードのリマインダーは JSON で保存する)ので、この変更は SQL Server だけ。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。
-- 流す前の DB で、新しいアプリが未対応に戻す操作をすると、検査に当たって保存できない。

-- Status の値の範囲を (1, 2)から (0, 1, 2)に広げる。検査は定義を変えられないので、付け替える。付け替える間に新しい行が入らないよう、同じバッチで続けて行う
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ReminderState_Status' AND parent_object_id = OBJECT_ID(N'dbo.ReminderState') AND definition NOT LIKE N'%(0)%')
BEGIN
    ALTER TABLE dbo.ReminderState DROP CONSTRAINT CK_ReminderState_Status;
    ALTER TABLE dbo.ReminderState ADD CONSTRAINT CK_ReminderState_Status CHECK (Status IN (0, 1, 2));
END
GO
