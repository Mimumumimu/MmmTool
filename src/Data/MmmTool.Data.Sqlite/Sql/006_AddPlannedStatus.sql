-- 作業管理の状態に「作業予定」を足し、状態のコード番号を、表示順にそろえて振り直す。
-- 設計は docs/specs/work-report.md の「状態「作業予定」の追加」と「記録を作業ごと・日ごとに 1 件にする」。
-- やること: ① 状態のコード番号を 未対応 1 / 作業予定 2 / 作業中 3 / 完了 4 にする (コードマスタと、作業の StatusCodeNo)
--           ② 日ごとの記録の一意の索引を (WorkItemId, Date) に作り替える ③ 作業の担当者の索引を足す
-- データベースの版が 5 のときだけ、アプリが 1 つのトランザクションで実行する。作成者・更新者は 1 (ローカルのユーザー)。
-- 古い作成スクリプト (001・002)は直さない。新しく作った DB は、002 で 1〜3 を入れたあと、この移行で 4 つにそろう。

-- ① 一意の索引に当たらないよう、完了 (3 → 4)、作業中 (2 → 3)、作業予定の追加 (2)の順に、別の文で行う
UPDATE CodeMaster
SET CodeNo = 4, SortOrder = 4, UpdatedAt = strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', UpdatedByUserId = 1
WHERE FeatureNo = 2 AND CategoryNo = 1 AND CodeNo = 3;

UPDATE CodeMaster
SET CodeNo = 3, SortOrder = 3, UpdatedAt = strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', UpdatedByUserId = 1
WHERE FeatureNo = 2 AND CategoryNo = 1 AND CodeNo = 2;

INSERT INTO CodeMaster (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, FeatureNo, CategoryNo, CodeNo, SortOrder, NumericValue, Name)
VALUES (0,
        strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
        strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', 1,
        2, 1, 2, 2, 0, '作業予定');

UPDATE WorkItem
SET StatusCodeNo = CASE StatusCodeNo WHEN 3 THEN 4 WHEN 2 THEN 3 END,
    UpdatedAt = strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00', UpdatedByUserId = 1
WHERE StatusCodeNo IN (2, 3);

-- ② 日ごとの記録は、作業ごと・日ごとに 1 件 (人は区別しない。論理削除された行も含めて一意)
DROP INDEX IF EXISTS UX_WorkRecord_WorkItemId_Date_CreatedByUserId;
CREATE UNIQUE INDEX UX_WorkRecord_WorkItemId_Date ON WorkRecord (WorkItemId, Date);

-- ③ 作業報告の実績・予定 (担当が自分の作業を探す)。削除されていない行だけ
CREATE INDEX IX_WorkItem_AssigneeUserId_StatusCodeNo ON WorkItem (AssigneeUserId, StatusCodeNo) WHERE IsDeleted = 0;
