-- 版 5: 機能番号を、サイドバーの初期の並びに付け直す。作業管理の機能番号を 2 から 1 に変える。設計は docs/specs/database.md の「機能番号」。
-- 機能番号 2 の行が無ければ、何もしない。DELETE 文は含まない。SQL Server の 008_RenumberFeatureNo.sql と同じ設計。
-- ローカルモードにあるのは、コードマスタ CodeMaster の作業管理の選択肢 (状態・進捗度・優先度)だけ。ユーザーごとの設定の表は SQL Server だけにある。

-- 作業管理の機能番号を 2 から 1 に変える。移した先に同じ区分・コード番号の行が既にあるときは、動かさない
UPDATE CodeMaster
SET FeatureNo = 1,
    UpdatedAt = strftime('%Y-%m-%dT%H:%M:%f', 'now', '+9 hours') || '+09:00'
WHERE FeatureNo = 2
  AND NOT EXISTS (SELECT 1 FROM CodeMaster c WHERE c.FeatureNo = 1 AND c.CategoryNo = CodeMaster.CategoryNo AND c.CodeNo = CodeMaster.CodeNo);
