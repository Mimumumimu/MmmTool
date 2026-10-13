-- 版 2: 日報メモの表 (WorkDayNote) を足す。設計は docs/specs/work-items.md の「日報メモ」と docs/specs/database.md。
-- 何度流しても壊れない (無いものだけを作る)。リリースまで、この版の変更はこのファイルに集める。

-- 日報メモ。作業に結びつかない、その人の 1 日分の記入 (打ち合わせなど)。1 人 1 日 1 件。持ち主は業務の列 UserId で持つ (CreatedByUserId は条件に使わない)
-- 空にしたときは、行を消さず Note を空文字にする (論理削除しない)。IsDeleted は共通ヘッダーの列で、この表の操作では使わない
CREATE TABLE IF NOT EXISTS WorkDayNote
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    UserId          INTEGER NOT NULL CHECK (UserId >= 1),
    Date            TEXT    NOT NULL,
    Note            TEXT    NOT NULL DEFAULT '' CHECK (length(Note) <= 2000)
);

-- 1 人 1 日 1 件 (同じ日に書き直したときは、この行を使い回す。日付の範囲の読み込み・直近の日の検索にも使う)
CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkDayNote_UserId_Date ON WorkDayNote (UserId, Date);
