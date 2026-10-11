-- 作業管理の「選んだ案件」の表 (WorkProjectSelection)を、捨てる。
-- 選んだ案件は、DB ではなく、PC の設定 (Data/AppSettings.json の WorkItems.SelectedProjects.sqlite.1)に持つ形に変わった (docs/specs/work-items.md の「表示する案件」)。
-- 表の中身 (どの案件を選んでいたか)は、消える。選び直す。データベースの版が 3 のときだけ、アプリが 1 つのトランザクションで実行する。
DROP TABLE IF EXISTS WorkProjectSelection;
