-- MmmTool の DB (SQL Server)。作業管理のコードマスタの初期値 (機能番号 2)を入れる。004_CreateCodeMaster.sql のあとに流す。
-- 何度流しても壊れない (無いコードだけを入れる)。DELETE 文は含まない。台帳は MmmTool.CodeMaster.Core の CodeFeatures と MmmTool.WorkItems.Core の WorkCodes。

-- 初期値 (作業管理 = 機能番号 2)。無いコードだけを入れる。作成者・更新者は 0 (特定のユーザーではない)
-- 区分 1: 状態 / 区分 2: 進捗度 (コード番号 1〜11 が 0%〜100%。70・80・90 だけ名前がある) / 区分 3: 優先度
INSERT INTO dbo.CodeMaster (IsDeleted, CreatedByUserId, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0, 0, 0, 2, v.CategoryNo, v.CodeNo, v.CodeNo, v.NumericValue, v.Name
FROM (VALUES
    (1, 1, 0, N'未対応'), (1, 2, 0, N'作業中'), (1, 3, 0, N'完了'),
    (2, 1, 0, N''), (2, 2, 10, N''), (2, 3, 20, N''), (2, 4, 30, N''), (2, 5, 40, N''), (2, 6, 50, N''), (2, 7, 60, N''),
    (2, 8, 70, N'作成完了'), (2, 9, 80, N'内部レビュー完了'), (2, 10, 90, N'外部レビュー完了'), (2, 11, 100, N''),
    (3, 1, 0, N'高'), (3, 2, 0, N'中'), (3, 3, 0, N'低')
) AS v (CategoryNo, CodeNo, NumericValue, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CodeMaster c WHERE c.FeatureNo = 2 AND c.CategoryNo = v.CategoryNo AND c.CodeNo = v.CodeNo);
GO
