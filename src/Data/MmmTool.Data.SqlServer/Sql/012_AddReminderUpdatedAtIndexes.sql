-- 版 12: リマインダーの毎分の読み直しを、差分 (前回より後に更新された行だけ)にするために、更新日時の索引を足す。設計は docs/specs/database.md の「Repository の動き」と docs/specs/reminders.md。
-- 何度流しても壊れない。無いものだけを作る。DELETE 文は含まない。既存の行は変わらない。
-- SQLite には Reminder と ReminderState の表が無い (ローカルモードのリマインダーは JSON で保存する)ので、この変更は SQL Server だけ。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- リマインダーの本体。見える範囲 (宛先が自分か全員宛て、または自分が作成)の絞り込みは、更新日時で絞った行に対して行う。論理削除された行も含めて探す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.Reminder'))
CREATE INDEX IX_Reminder_UpdatedAt ON dbo.Reminder (UpdatedAt);
GO

-- 自分の対応状態。業務の列 UserId で絞り、更新日時の順に探す
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReminderState_UserId_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.ReminderState'))
CREATE INDEX IX_ReminderState_UserId_UpdatedAt ON dbo.ReminderState (UserId, UpdatedAt);
GO
