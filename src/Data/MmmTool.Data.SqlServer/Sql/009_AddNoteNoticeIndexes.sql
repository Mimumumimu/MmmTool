-- 版 9: 他の人の作成・更新の通知のための索引を 2 つ足す。設計は docs/specs/notes.md の「他の人の作成・更新の通知」と docs/specs/database.md。
-- 何度流しても壊れない。無いものだけを作る。DELETE 文は含まない。SQLite の Note の索引は、あるべき形の schema.sql が足す。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- 前回読んだ時刻の後に更新されたノートを、全件を調べずに探す。MmmBatch が 1 秒おきに、PC 版が 10 秒おきに読む。論理削除された行も含める
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Note_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.Note'))
CREATE INDEX IX_Note_UpdatedAt ON dbo.Note (UpdatedAt);
GO

-- 「ノートの通知を受け取る」がオンの人を、人の全件を調べずに、人の番号の順に探す。MmmBatch が、ノートの更新のたびに読む。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUserSetting_FeatureNo_CategoryNo_CodeNo_Value_UserId' AND object_id = OBJECT_ID(N'dbo.AppUserSetting'))
CREATE INDEX IX_AppUserSetting_FeatureNo_CategoryNo_CodeNo_Value_UserId ON dbo.AppUserSetting (FeatureNo, CategoryNo, CodeNo, Value, UserId) WHERE IsDeleted = 0;
GO
