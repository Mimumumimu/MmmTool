-- 作業 (WorkItem)の列名 CompletedOn を、他の日付の列 (StartDate・DueDate)と語尾をそろえて、CompletedDate に直す。
-- 設計は docs/specs/work-items.md。データベースの版が 4 のときだけ、アプリが 1 つのトランザクションで実行する。
-- 列名を変えると、インデックスの中の列名も追従する。インデックスの名前は変わらないので、作り直す。
ALTER TABLE WorkItem RENAME COLUMN CompletedOn TO CompletedDate;

DROP INDEX IF EXISTS IX_WorkItem_ProjectId_CompletedOn;

-- 案件の完了の計算 (削除されていない行だけ)
CREATE INDEX IX_WorkItem_ProjectId_CompletedDate ON WorkItem (ProjectId, CompletedDate) WHERE IsDeleted = 0;
