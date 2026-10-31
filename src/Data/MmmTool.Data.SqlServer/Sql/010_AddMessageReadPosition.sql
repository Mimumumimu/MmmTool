-- 版 10: メッセージの既読 (相手に見える既読)のために、参加者の表に「読んだ最後のメッセージの番号」の列と、更新日時の索引を足す。設計は docs/specs/messages.md の「既読」と docs/specs/database.md。
-- 何度流しても壊れない。無いものだけを作る。DELETE 文は含まない。既存の行は 0 (何も読んでいない)になる。
-- SQLite にはメッセージの表が無いので、この変更は SQL Server だけ。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 参加者ごとの、読んだ最後のメッセージの Id。0 は何も読んでいない。大きくなるときだけ書く。表の末尾 (UserId の次)に付く
IF COL_LENGTH(N'dbo.MessageConversationMember', N'LastReadMessageId') IS NULL
ALTER TABLE dbo.MessageConversationMember ADD LastReadMessageId int NOT NULL
    CONSTRAINT DF_MessageConversationMember_LastReadMessageId DEFAULT (0)
    CONSTRAINT CK_MessageConversationMember_LastReadMessageId CHECK (LastReadMessageId >= 0);
GO

-- Web 版のサーバーが、更新された参加者の行 (読んだ位置の変わり)を、人を特定せずに、全員分まとめて調べる。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MessageConversationMember_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.MessageConversationMember'))
CREATE INDEX IX_MessageConversationMember_UpdatedAt ON dbo.MessageConversationMember (UpdatedAt);
GO
