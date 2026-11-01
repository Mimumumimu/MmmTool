-- 版 13: 作業管理の状態に「保留」を足す。コード番号は 5 で、並びは作業中の次、完了の前にする。設計は docs/specs/work-items.md の「コードマスタ」。
-- コードマスタ CodeMaster の作業管理 (機能番号 1)・区分 1 (状態)の、完了 (コード番号 4)の表示順を 4 から 5 に変え、保留 (コード番号 5)を表示順 4 で足す。
-- 何度流しても同じ結果になる。DELETE 文は含まない。
-- SQLite の同じ設計は、Sqlite の Sql/006_AddWorkStatusOnHold.sql。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから実行する。sqlcmd は -I と -f 65001 を付ける。

-- 完了の表示順を後ろにずらす。表示順が 4 のままの行だけ変える
UPDATE dbo.CodeMaster
SET SortOrder = 5, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
WHERE FeatureNo = 1 AND CategoryNo = 1 AND CodeNo = 4 AND SortOrder = 4;
GO

-- 保留を足す。コード番号 5 の行が無いときだけ入れる。作成者・更新者は 0 (特定のユーザーではない)
INSERT INTO dbo.CodeMaster (IsDeleted, CreatedByUserId, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0, 0, 0, 1, 1, 5, 4, 0, N'保留'
WHERE NOT EXISTS (SELECT 1 FROM dbo.CodeMaster c WHERE c.FeatureNo = 1 AND c.CategoryNo = 1 AND c.CodeNo = 5);
GO
