-- MmmTool の DB (SQL Server)。作業管理の「選んだ案件」の表 (dbo.WorkProjectSelection)を、捨てる。
-- 選んだ案件は、DB ではなく、各 PC の設定 (Data/AppSettings.json の WorkItems.SelectedProjects.{保存先}.{ユーザーの番号})に持つ形に変わった (docs/specs/work-items.md の「表示する案件」)。
-- 何度流しても壊れない (表があるときだけ捨てる)。表の中身 (どの案件を選んでいたか)は、消える。各 PC で、選び直す。
-- DELETE 文は含まない。DROP TABLE は、ユーザーの許可を取ってから流す。
IF OBJECT_ID(N'dbo.WorkProjectSelection', N'U') IS NOT NULL
DROP TABLE dbo.WorkProjectSelection;
GO
