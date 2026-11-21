-- ノートの追加の値 NoteAttribute の枝番の列を BranchNo にして、1 行の中の項目の列 CodeNo を足す。設計は docs/specs/database.md の NoteAttribute。
-- 今の CodeNo (同じ種類の中の枝番)を、BranchNo に名前を変える。値と列の位置は変わらない。
-- 新しい CodeNo は、1 行の中の項目の番号で、1 は日付。既定値 1 で足すので、今ある行は全部、日付の項目になる。UPDATE と DELETE は含まない。
-- 新しい CodeNo は表の末尾に付く。列の並び (BranchNo の次、Value の前)は、SSMS で直す。
-- 一意の索引と、今日の日付を探す索引を、枝番と項目を含む形に作り直す。古い索引は消す。
-- 何度流しても壊れない形にしている。SQLite の 007_DropNoteAttributeOldIndexes.sql と schema.sql が、同じ設計。

-- 列を参照する検査があると、sp_rename は列の名前を変えられない。検査を先に外し、名前を変えてから、BranchNo の検査として付け直す
IF COL_LENGTH(N'dbo.NoteAttribute', N'BranchNo') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.CK_NoteAttribute_CodeNo', N'C') IS NOT NULL
        ALTER TABLE dbo.NoteAttribute DROP CONSTRAINT CK_NoteAttribute_CodeNo;
    IF OBJECT_ID(N'dbo.CK_NoteAttribute_BranchNo', N'C') IS NOT NULL
        ALTER TABLE dbo.NoteAttribute DROP CONSTRAINT CK_NoteAttribute_BranchNo;
    EXEC sp_rename N'dbo.NoteAttribute.CodeNo', N'BranchNo', N'COLUMN';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_NoteAttribute_BranchNo' AND parent_object_id = OBJECT_ID(N'dbo.NoteAttribute'))
ALTER TABLE dbo.NoteAttribute ADD CONSTRAINT CK_NoteAttribute_BranchNo CHECK (BranchNo >= 1);
GO

IF COL_LENGTH(N'dbo.NoteAttribute', N'CodeNo') IS NULL
ALTER TABLE dbo.NoteAttribute ADD CodeNo int NOT NULL
    CONSTRAINT DF_NoteAttribute_CodeNo DEFAULT (1)
    CONSTRAINT CK_NoteAttribute_CodeNo CHECK (CodeNo >= 1);
GO

-- 古い索引を消す。名前の変更で、列は BranchNo に変わっているが、索引の名前は古いまま残る
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NoteAttribute_NoteId_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.NoteAttribute'))
DROP INDEX UX_NoteAttribute_NoteId_CategoryNo_CodeNo ON dbo.NoteAttribute;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NoteAttribute_CategoryNo_Value_NoteId' AND object_id = OBJECT_ID(N'dbo.NoteAttribute'))
DROP INDEX IX_NoteAttribute_CategoryNo_Value_NoteId ON dbo.NoteAttribute;
GO

-- ノートごと・種類ごと・枝番ごと・項目ごとに 1 行にする。論理削除された行も含めて一意で、ノートの値を、削除済みも含めて番号から探すのにも使う
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NoteAttribute_NoteId_CategoryNo_BranchNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.NoteAttribute'))
CREATE UNIQUE INDEX UX_NoteAttribute_NoteId_CategoryNo_BranchNo_CodeNo ON dbo.NoteAttribute (NoteId, CategoryNo, BranchNo, CodeNo);
GO

-- 実際の今日と表示する日がほかの実施日のノートを、全件を走査せずに探す。項目を CodeNo で絞るので、別の項目の値を日付と取り違えない。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NoteAttribute_CategoryNo_CodeNo_Value_NoteId' AND object_id = OBJECT_ID(N'dbo.NoteAttribute'))
CREATE INDEX IX_NoteAttribute_CategoryNo_CodeNo_Value_NoteId ON dbo.NoteAttribute (CategoryNo, CodeNo, Value, NoteId) WHERE IsDeleted = 0;
GO
