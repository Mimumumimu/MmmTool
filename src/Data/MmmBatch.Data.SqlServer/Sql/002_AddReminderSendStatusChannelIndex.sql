-- 版 2: 送信の状況の表 ReminderSendStatus に、送信先ごとに新しい順に引く索引を足す。設計は docs/specs/database.md と docs/specs/web.md。
-- 何度流しても壊れない。無ければ作る。リリースまで、この版の変更はこのファイルに集める。DELETE 文は含まない。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。

-- Web 版の「受け取った通知」の一覧が、自分の送信先に届いたリマインダーを、送った日時の新しい順に引く。表は掃除されず増え続けるので、索引で絞る。業務の列だけで作る
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReminderSendStatus_ChannelId_SentAt' AND object_id = OBJECT_ID(N'dbo.ReminderSendStatus'))
CREATE INDEX IX_ReminderSendStatus_ChannelId_SentAt ON dbo.ReminderSendStatus (ChannelId, SentAt DESC);
GO
