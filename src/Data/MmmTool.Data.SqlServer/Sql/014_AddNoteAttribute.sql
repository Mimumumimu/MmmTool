-- ノートの追加の値。1 行に 1 つの値を持つ縦持ちで、AppUserSetting の UserId を、ノートの番号 NoteId に置き換えた作り。
-- CategoryNo は値の種類で、1 はほかの実施日。ほかの実施日の Value は yyyy-MM-dd の日付。CodeNo は同じ種類の中の枝番で、値ごとに別の番号にする。
-- 値を外すときは IsDeleted = 1 にして、DELETE はしない。NoteId の存在と、日付が期間の中にあることは、アプリが検査する。外部キーは付けない。
IF OBJECT_ID(N'dbo.NoteAttribute', N'U') IS NULL
CREATE TABLE dbo.NoteAttribute
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_NoteAttribute PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_NoteAttribute_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NoteAttribute_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_NoteAttribute_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NoteAttribute_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_NoteAttribute_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    NoteId          int             NOT NULL CONSTRAINT CK_NoteAttribute_NoteId CHECK (NoteId >= 1),
    CategoryNo      int             NOT NULL CONSTRAINT CK_NoteAttribute_CategoryNo CHECK (CategoryNo >= 1),
    CodeNo          int             NOT NULL CONSTRAINT CK_NoteAttribute_CodeNo CHECK (CodeNo >= 1),
    Value           nvarchar(200)   NOT NULL CONSTRAINT DF_NoteAttribute_Value DEFAULT (N'')
);
GO

-- ノートごと・種類ごと・枝番ごとに 1 行にする。論理削除された行も含めて一意で、ノートの値を、削除済みも含めて番号から探すのにも使う
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NoteAttribute_NoteId_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.NoteAttribute'))
CREATE UNIQUE INDEX UX_NoteAttribute_NoteId_CategoryNo_CodeNo ON dbo.NoteAttribute (NoteId, CategoryNo, CodeNo);
GO

-- 実際の今日と表示する日がほかの実施日のノートを、全件を走査せずに探す。削除されていない行だけ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NoteAttribute_CategoryNo_Value_NoteId' AND object_id = OBJECT_ID(N'dbo.NoteAttribute'))
CREATE INDEX IX_NoteAttribute_CategoryNo_Value_NoteId ON dbo.NoteAttribute (CategoryNo, Value, NoteId) WHERE IsDeleted = 0;
GO
