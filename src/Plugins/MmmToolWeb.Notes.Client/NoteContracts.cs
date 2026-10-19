namespace MmmToolWeb.Notes.Client;

/// <summary>
/// 一覧に出す、ノート 1 件。
/// </summary>
/// <param name="Id">ノートの番号。</param>
/// <param name="Title">タイトル。</param>
/// <param name="Body">本文。書いたとおりの文字で、編集のときに使う。</param>
/// <param name="BodyHtml">本文を、表示してよい HTML にしたもの。サーバーが組み立てる。</param>
/// <param name="Priority">重要度。1 から 5 で、大きいほど重要。</param>
/// <param name="StartDate">開始日。</param>
/// <param name="EndDate">終了日。無期限のときは null。</param>
/// <param name="IsPrivate">書いた人だけに見えるか。</param>
/// <param name="IsDeleted">削除されているか。</param>
/// <param name="IsMine">自分が書いたか。公開範囲を変えられるのは、書いた人だけ。</param>
/// <param name="AuthorName">書いた人の名前。</param>
/// <param name="UpdatedByName">更新した人の名前。書いた人と同じときは null。</param>
/// <param name="UpdatedAtText">更新日時の表示。サーバーの時計で、分までの文字にしてある。ブラウザ上の .NET は端末のタイムゾーンを知らないため。</param>
/// <param name="IsUpdatedToday">更新が今日か。</param>
public sealed record NoteDto(
    int Id,
    string Title,
    string Body,
    string BodyHtml,
    int Priority,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsPrivate,
    bool IsDeleted,
    bool IsMine,
    string AuthorName,
    string? UpdatedByName,
    string UpdatedAtText,
    bool IsUpdatedToday);

/// <summary>
/// ある日に有効なノートの一覧。重要度の高い順に並んでいる。
/// </summary>
/// <param name="Day">日。</param>
/// <param name="Notes">その日に有効なノート。</param>
public sealed record NoteListResponse(DateOnly Day, IReadOnlyList<NoteDto> Notes);

/// <summary>
/// 内容の選択肢 1 つ。
/// </summary>
/// <param name="Key">選択肢の識別。</param>
/// <param name="Label">画面に出す名前。</param>
/// <param name="Default">既定値。</param>
public sealed record DayContentOptionDto(string Key, string Label, bool Default);

/// <summary>
/// ノートの画面に出せる、他の機能の内容 (作業報告など) 1 つ。
/// </summary>
/// <param name="Key">内容の識別。</param>
/// <param name="Title">画面に出す名前。</param>
/// <param name="DefaultFrom">実績の開始日の既定。</param>
/// <param name="Options">真偽の選択肢。</param>
public sealed record DayContentSourceDto(string Key, string Title, DateOnly DefaultFrom, IReadOnlyList<DayContentOptionDto> Options);

/// <summary>
/// 出せる内容の一覧。
/// </summary>
/// <param name="Sources">出せる内容。サーバーに登録された機能の分だけ。</param>
public sealed record DayContentSourceListResponse(IReadOnlyList<DayContentSourceDto> Sources);

/// <summary>
/// ノートのカードの上に出す、内容の枠 1 つ。
/// </summary>
/// <param name="Title">見出し。</param>
/// <param name="Text">文。改行と字下げは、そのまま出す。</param>
public sealed record DayContentFrameDto(string Title, string Text);

/// <summary>
/// 内容の枠の一覧。全員のときは、自分の枠が先頭で、あとは名前順。
/// </summary>
/// <param name="Frames">枠。</param>
public sealed record DayContentFramesResponse(IReadOnlyList<DayContentFrameDto> Frames);

/// <summary>
/// ノートを保存する要求。新規の登録と編集で同じ。
/// </summary>
/// <param name="Title">タイトル。</param>
/// <param name="Body">本文。</param>
/// <param name="Priority">重要度。1 から 5。</param>
/// <param name="IsPrivate">書いた人だけに見えるか。</param>
/// <param name="StartDate">開始日。</param>
/// <param name="EndDate">終了日。無期限のときは null。</param>
public sealed record NoteSaveRequest(string Title, string Body, int Priority, bool IsPrivate, DateOnly StartDate, DateOnly? EndDate);

/// <summary>
/// ノートを削除する・復元する要求。
/// </summary>
/// <param name="IsDeleted">削除するなら true、復元するなら false。</param>
public sealed record NoteDeletedRequest(bool IsDeleted);
