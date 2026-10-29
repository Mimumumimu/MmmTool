-- 版 8: 機能番号を、サイドバーの初期の並びに付け直し、メッセージの受け取り先を共通の 0 に移す。設計は docs/specs/database.md の AppUserSetting の節。
-- 機能番号の付け直しの部分は、1 度だけ流す。番号が入れ替わるので、流し直すと、新しい番号の行を書き換えてしまう。検査の付け替えは、何度流しても壊れない。DELETE 文は含まない。
-- 流すのは、MmmTool・Web 版・MmmBatch・管理ツールを新しい版に入れ替えるのと同時にする。古い版のアプリは、番号の意味が食い違い、作業管理の選択肢が空になり、管理者の設定が別の機能の設定として読まれる。
-- SQLite の同じ設計は、Sqlite の Sql/005_RenumberFeatureNo.sql。
-- 使い方: 001_CreateTables.sql と同じ。DB を選んでから、表を作れる権限のあるユーザーで実行する。sqlcmd は -I と -f 65001 を付ける。

-- ユーザーごとの設定 AppUserSetting の機能番号に、どの機能にも属さない共通の 0 を使えるようにする。機能番号の検査を「1 以上」から「0 以上」に付け替える。
-- 検査は定義を変えられないので付け替える。付け替える間に新しい行が入らないよう、同じバッチで続けて行う。0 を許す検査が既にあれば、何もしない。
-- 管理者の設定 AppUserAdminSetting とコードマスタ CodeMaster の検査は、1 以上のまま
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AppUserSetting_FeatureNo' AND parent_object_id = OBJECT_ID(N'dbo.AppUserSetting') AND definition NOT LIKE N'%>=(0)%')
BEGIN
    ALTER TABLE dbo.AppUserSetting DROP CONSTRAINT CK_AppUserSetting_FeatureNo;
    ALTER TABLE dbo.AppUserSetting ADD CONSTRAINT CK_AppUserSetting_FeatureNo CHECK (FeatureNo >= 0);
END
GO

-- 機能番号を、サイドバーの初期の並びに付け直す。共通 0・作業管理 1・ノート 2・CLI補助 3・リマインダー 4・メッセージ 5・リンク 6・クリップボード転送 7・Backlog 8・ツール 9。台帳は MmmTool.CodeMaster.Core の CodeFeatures。
-- 書き換えるのは、旧番号 作業管理 2・メッセージ 3・ノート 4・リマインダー 5 を持つ行。
-- 書き換える順は、移し先が空いている順にする。管理者の設定は 2→1、4→2、5→4、3→5 の順で、一意の索引にぶつからないよう、この順を変えない。1 つのトランザクションで行う。
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRAN;

    -- コードマスタの作業管理の選択肢 (状態・進捗度・優先度)を、機能番号 2 から 1 にする
    UPDATE dbo.CodeMaster SET FeatureNo = 1, UpdatedAt = SYSDATETIMEOFFSET() WHERE FeatureNo = 2;

    -- メッセージの受け取り先を、メッセージ 3 から共通 0 にする。区分 1・コード 1 はそのまま。移し先に削除されていない同じ設定がある人の行は動かさない
    UPDATE s SET FeatureNo = 0, UpdatedAt = SYSDATETIMEOFFSET()
    FROM dbo.AppUserSetting AS s
    WHERE s.FeatureNo = 3 AND s.CategoryNo = 1 AND s.CodeNo = 1
      AND (s.IsDeleted = 1 OR NOT EXISTS (
            SELECT 1 FROM dbo.AppUserSetting AS t
            WHERE t.UserId = s.UserId AND t.FeatureNo = 0 AND t.CategoryNo = 1 AND t.CodeNo = 1 AND t.IsDeleted = 0));

    -- 管理者の設定 (機能の利用)の機能番号を付け直す
    UPDATE dbo.AppUserAdminSetting SET FeatureNo = 1, UpdatedAt = SYSDATETIMEOFFSET() WHERE FeatureNo = 2;
    UPDATE dbo.AppUserAdminSetting SET FeatureNo = 2, UpdatedAt = SYSDATETIMEOFFSET() WHERE FeatureNo = 4;
    UPDATE dbo.AppUserAdminSetting SET FeatureNo = 4, UpdatedAt = SYSDATETIMEOFFSET() WHERE FeatureNo = 5;
    UPDATE dbo.AppUserAdminSetting SET FeatureNo = 5, UpdatedAt = SYSDATETIMEOFFSET() WHERE FeatureNo = 3;

    COMMIT TRAN;
END
GO
