-- 版 3: ノートの表 (Note) を足す。設計は docs/specs/notes.md と docs/specs/database.md。
-- 何度流しても壊れない (無いものだけを作る)。リリースまで、この版の変更はこのファイルに集める。

-- ノート。開始日から終了日までの期間・タイトル・本文・重要度・公開範囲を持つ。書いた人は業務の列 AuthorUserId で持つ (CreatedByUserId は条件に使わない)。
-- ローカルモードは、書いた人が常に 1 (固定のローカルユーザー)で、IsPrivate は常に 0。終了日が無い (無期限)ときは 9999-12-31。本文は上限を決めない
CREATE TABLE IF NOT EXISTS Note
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    IsDeleted       INTEGER NOT NULL DEFAULT 0 CHECK (IsDeleted IN (0, 1)),
    CreatedAt       TEXT    NOT NULL,
    CreatedByUserId INTEGER NOT NULL CHECK (CreatedByUserId >= 0),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedByUserId INTEGER NOT NULL CHECK (UpdatedByUserId >= 0),
    AuthorUserId    INTEGER NOT NULL CHECK (AuthorUserId >= 1),
    IsPrivate       INTEGER NOT NULL DEFAULT 0 CHECK (IsPrivate IN (0, 1)),
    StartDate       TEXT    NOT NULL,
    EndDate         TEXT    NOT NULL DEFAULT '9999-12-31' CHECK (EndDate >= StartDate),
    Priority        INTEGER NOT NULL DEFAULT 3 CHECK (Priority BETWEEN 1 AND 5),
    Title           TEXT    NOT NULL CHECK (length(Title) BETWEEN 1 AND 100),
    Body            TEXT    NOT NULL DEFAULT ''
);

-- 表示する日に有効な行 (EndDate >= その日 かつ StartDate <= その日) を探す。終了日を先頭にして、過去に終わった大量のノートを走査しない。削除された行も含める
CREATE INDEX IF NOT EXISTS IX_Note_EndDate_StartDate ON Note (EndDate, StartDate);
