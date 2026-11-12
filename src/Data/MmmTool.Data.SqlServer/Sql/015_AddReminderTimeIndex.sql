-- リマインダーの日付と送信時刻の索引。MmmBatch のリマインダー送信が、毎分、今日発動し、時刻が前回の実行の終わりから今までの範囲にあるリマインダーだけを探すのに使う。
-- 日付を先頭にするのは、過去の日付の 1 回きりのリマインダーを読まないため。曜日指定の行は日付が 9999-12-31 なので、今日の日付と 9999-12-31 の 2 つの範囲で探せる。
-- 曜日を最後に含めるのは、曜日に合わない行を、表を引く前に外すため。削除されていない行だけ。業務の列だけで作る。
-- リマインダーは SQLite (ローカルモード)に表が無いので、SQLite には足さない。DELETE 文は含まない。何度流しても壊れない。
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reminder_Date_Time_Weekdays' AND object_id = OBJECT_ID(N'dbo.Reminder'))
CREATE INDEX IX_Reminder_Date_Time_Weekdays ON dbo.Reminder (Date, Time, Weekdays) WHERE IsDeleted = 0;
GO
