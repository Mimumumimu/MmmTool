-- リマインダーの送信の状況の索引。MmmBatch が、毎分、今日と昨日の「送り終えていない」行 (送信中のまま止まった・失敗で回数の上限に達していない)だけを探すのに使う。
-- キーは日付・状態・回数・リマインダー・送信先の順。回数の上限の判断と、送信設定との結合の相手を、表を読まずに索引だけで引くため。業務の列だけで作る。
-- 表は MmmBatch の DB だけにあり、SQLite (ローカルモード)には無い。DELETE 文は含まない。何度流しても壊れない。
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReminderSendStatus_Date_Status_Attempts' AND object_id = OBJECT_ID(N'dbo.ReminderSendStatus'))
CREATE INDEX IX_ReminderSendStatus_Date_Status_Attempts ON dbo.ReminderSendStatus (Date, Status, Attempts, ReminderId, ChannelId);
GO
