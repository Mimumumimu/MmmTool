-- 版 6: 作業管理の状態に「保留」を足す。コード番号は 5 で、並びは作業中の次、完了の前にする。設計は docs/specs/work-items.md の「コードマスタ」。
-- コードマスタ CodeMaster の作業管理 (機能番号 1)・区分 1 (状態)の、完了 (コード番号 4)の表示順を 4 から 5 に変え、保留 (コード番号 5)を表示順 4 で足す。
-- 何度流しても同じ結果になる。DELETE 文は含まない。SQL Server の 013_AddWorkStatusOnHold.sql と同じ設計。

-- 完了の表示順を後ろにずらす。表示順が 4 のままの行だけ変える
UPDATE CodeMaster
SET SortOrder = 5,
    UpdatedAt = strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00',
    UpdatedByUserId = 1
WHERE FeatureNo = 1 AND CategoryNo = 1 AND CodeNo = 4 AND SortOrder = 4;

-- 保留を足す。コード番号 5 の行が無いときだけ入れる。作成者・更新者は 1 (ローカルのユーザー)
INSERT INTO CodeMaster (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
SELECT 0,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
       1, 1, 5, 4, 0, '保留'
WHERE NOT EXISTS (SELECT 1 FROM CodeMaster c WHERE c.FeatureNo = 1 AND c.CategoryNo = 1 AND c.CodeNo = 5);
