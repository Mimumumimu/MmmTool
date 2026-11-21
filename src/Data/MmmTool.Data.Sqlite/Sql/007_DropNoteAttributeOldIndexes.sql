-- 版 7: ノートの追加の値 NoteAttribute の古い索引を消す。設計は docs/specs/database.md の NoteAttribute。
-- 枝番の列を BranchNo にして、行の中の項目の列 CodeNo を足したので、枝番と項目を含む新しい索引に置き換える。新しい索引は、schema.sql の点検が作る。
-- SQLite の点検は、同じ名前の索引があると中身を比べずに飛ばし、古い索引を消さない。そのため、古い名前の索引だけを、ここで消す。DELETE 文は含まない。SQL Server の 016_AddNoteAttributeBranchNo.sql と同じ設計。
-- 索引が無い DB (新しい DB・まだ NoteAttribute を持たない DB)では、何もしない。

DROP INDEX IF EXISTS UX_NoteAttribute_NoteId_CategoryNo_CodeNo;
DROP INDEX IF EXISTS IX_NoteAttribute_CategoryNo_Value_NoteId;
