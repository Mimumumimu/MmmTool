-- MmmTool の DB (SQL Server)。コードマスタ (CodeMaster)を、dbo スキーマに作る。機能をまたいで使う、選択肢などの表。
-- 設計は docs/specs/database.md の「コードマスタの表」と docs/specs/work-items.md の「コードマスタ」。何度流しても壊れない (無いものだけを作る)。DELETE 文は含まない。
-- 使い方: 001 のあとに、同じ DB を選んで、表を作れる権限のあるユーザーで、全体を実行する。初期値の行は、機能ごとの別のスクリプト (作業リストは 005_SeedWorkItemCodes.sql)が入れる。
-- 型: 日時は datetimeoffset(3)。外部キーは使わない。NULL は使わない。

-- コードマスタ。(FeatureNo, CategoryNo, CodeNo) で 1 件。FeatureNo は機能番号 (台帳は MmmTool.CodeMaster.Core の CodeFeatures。作業リストは 2)、
-- CategoryNo は機能の中の区分 (コードの種類。作業リストは 1 = 状態・2 = 進捗度・3 = 優先度)、CodeNo は区分の中の識別子 (一度使った番号は、同じ意味のまま変えない)
-- NumericValue は計算に使う数 (進捗度の % など。要らないコードは 0)、Name の「ない」は空文字
-- 全員に影響するので、画面からは編集しない (この表を SQL で直す。選択肢を外すときは IsDeleted = 1。DELETE はしない)
IF OBJECT_ID(N'dbo.CodeMaster', N'U') IS NULL
CREATE TABLE dbo.CodeMaster
(
    Id              int             IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodeMaster PRIMARY KEY,
    IsDeleted       bit             NOT NULL CONSTRAINT DF_CodeMaster_IsDeleted DEFAULT (0),
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_CodeMaster_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
    CreatedByUserId int             NOT NULL CONSTRAINT CK_CodeMaster_CreatedByUserId CHECK (CreatedByUserId >= 0),
    UpdatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_CodeMaster_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
    UpdatedByUserId int             NOT NULL CONSTRAINT CK_CodeMaster_UpdatedByUserId CHECK (UpdatedByUserId >= 0),
    FeatureNo       int             NOT NULL CONSTRAINT CK_CodeMaster_FeatureNo CHECK (FeatureNo >= 1),
    CategoryNo      int             NOT NULL CONSTRAINT CK_CodeMaster_CategoryNo CHECK (CategoryNo >= 1),
    CodeNo          int             NOT NULL CONSTRAINT CK_CodeMaster_CodeNo CHECK (CodeNo >= 1),
    SortOrder       int             NOT NULL CONSTRAINT DF_CodeMaster_SortOrder DEFAULT (0) CONSTRAINT CK_CodeMaster_SortOrder CHECK (SortOrder >= 0),
    NumericValue    int             NOT NULL CONSTRAINT DF_CodeMaster_NumericValue DEFAULT (0),
    Name            nvarchar(100)   NOT NULL CONSTRAINT DF_CodeMaster_Name DEFAULT (N'')
);
GO

-- 論理削除された行も含めて一意 (選択肢から外した行を、同じコード番号で戻せる)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CodeMaster_FeatureNo_CategoryNo_CodeNo' AND object_id = OBJECT_ID(N'dbo.CodeMaster'))
CREATE UNIQUE INDEX UX_CodeMaster_FeatureNo_CategoryNo_CodeNo ON dbo.CodeMaster (FeatureNo, CategoryNo, CodeNo);
GO

-- 機能ごとの読み込み・差分の取得 (論理削除された行も含む)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CodeMaster_FeatureNo_UpdatedAt' AND object_id = OBJECT_ID(N'dbo.CodeMaster'))
CREATE INDEX IX_CodeMaster_FeatureNo_UpdatedAt ON dbo.CodeMaster (FeatureNo, UpdatedAt);
GO
