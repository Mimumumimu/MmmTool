namespace MmmToolWeb.Notes.Client;

/// <summary>一覧に出すノート 1 件</summary>
/// <param name="Id">ノートの番号</param>
/// <param name="Title">タイトル</param>
/// <param name="Body">
/// 本文。
/// 書いたとおりの文字で、編集のときに使う。
/// </param>
/// <param name="BodyHtml">
/// 本文を、表示してよい HTML にしたもの。
/// サーバーが組み立てる。
/// </param>
/// <param name="Priority">
/// 重要度。
/// 1 から 5 で、大きいほど重要。
/// </param>
/// <param name="StartDate">開始日</param>
/// <param name="EndDate">
/// 終了日
/// 無期限のときは null
/// </param>
/// <param name="EventDate">
/// 実施日
/// 未設定のときは null
/// </param>
/// <param name="OtherEventDates">
/// ほかの実施日。
/// 日付の順で、無いときは空。
/// </param>
/// <param name="IsEventDay">
/// 表示する日が実施日かほかの実施日のどれかか
/// カードを目立たせる
/// </param>
/// <param name="IsEventToday">
/// 表示する日が実際の今日でもあるか
/// 帯の文言を決める
/// </param>
/// <param name="IsPrivate">書いた人だけに見えるか</param>
/// <param name="IsDeleted">削除されているか</param>
/// <param name="IsMine">
/// 自分が書いたか
/// 公開範囲を変えられるのは書いた人だけ
/// </param>
/// <param name="AuthorName">書いた人の名前</param>
/// <param name="UpdatedByName">
/// 更新した人の名前
/// 書いた人と同じときは null
/// </param>
/// <param name="UpdatedAtText">
/// 更新日時の表示。
/// サーバーの時計で、分までの文字にしてある。
/// ブラウザ上の .NET は端末のタイムゾーンを知らないため。
/// </param>
/// <param name="IsUpdatedToday">更新が今日か</param>
/// <param name="Kind">
/// ノートの種類。
/// 0 は通常のノートで、1 は日程。
/// </param>
/// <param name="Schedule">
/// 日程。
/// 日付と開始の順で、種類が日程でないときは空か null。
/// </param>
public sealed record NoteDto(
    int Id,
    string Title,
    string Body,
    string BodyHtml,
    int Priority,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? EventDate,
    IReadOnlyList<DateOnly> OtherEventDates,
    bool IsEventDay,
    bool IsEventToday,
    bool IsPrivate,
    bool IsDeleted,
    bool IsMine,
    string AuthorName,
    string? UpdatedByName,
    string UpdatedAtText,
    bool IsUpdatedToday,
    int Kind = 0,
    IReadOnlyList<ScheduleItemDto>? Schedule = null);

/// <summary>日程の 1 件</summary>
/// <param name="Date">日付</param>
/// <param name="Start">
/// 開始の時刻で、HH:mm の形。
/// 無いときは null。
/// </param>
/// <param name="End">
/// 終了の時刻で、HH:mm の形。
/// 無いときは null。
/// </param>
/// <param name="Content">内容</param>
/// <param name="Remark">備考</param>
public sealed record ScheduleItemDto(DateOnly Date, string? Start, string? End, string Content, string Remark);

/// <summary>ある日に有効なノートの一覧</summary>
/// <param name="Day">日</param>
/// <param name="Notes">その日に有効なノート</param>
/// <remarks>重要度の高い順に並んでいる</remarks>
public sealed record NoteListResponse(DateOnly Day, IReadOnlyList<NoteDto> Notes);

/// <summary>内容の選択肢 1 つ</summary>
/// <param name="Key">選択肢の識別</param>
/// <param name="Label">画面に出す名前</param>
/// <param name="Default">既定値</param>
public sealed record DayContentOptionDto(string Key, string Label, bool Default);

/// <summary>ノートの画面に出せる他の機能の内容 1 つ</summary>
/// <param name="Key">内容の識別</param>
/// <param name="Title">画面に出す名前</param>
/// <param name="DefaultFrom">実績の開始日の既定</param>
/// <param name="Options">真偽の選択肢</param>
public sealed record DayContentSourceDto(string Key, string Title, DateOnly DefaultFrom, IReadOnlyList<DayContentOptionDto> Options);

/// <summary>出せる内容の一覧</summary>
/// <param name="Sources">
/// 出せる内容
/// サーバーに登録された機能の分だけ
/// </param>
public sealed record DayContentSourceListResponse(IReadOnlyList<DayContentSourceDto> Sources);

/// <summary>ノートのカードの上に出す内容の枠 1 つ</summary>
/// <param name="Title">見出し</param>
/// <param name="Text">
/// 文
/// 改行と字下げはそのまま出す
/// </param>
public sealed record DayContentFrameDto(string Title, string Text);

/// <summary>内容の枠の一覧</summary>
/// <param name="Frames">枠</param>
/// <remarks>全員のときは自分の枠が先頭で、あとは名前順。</remarks>
public sealed record DayContentFramesResponse(IReadOnlyList<DayContentFrameDto> Frames);

/// <summary>ノートを保存する要求</summary>
/// <param name="Title">タイトル</param>
/// <param name="Body">本文</param>
/// <param name="Priority">
/// 重要度
/// 1 から 5
/// </param>
/// <param name="IsPrivate">書いた人だけに見えるか</param>
/// <param name="StartDate">開始日</param>
/// <param name="EndDate">
/// 終了日
/// 無期限のときは null
/// </param>
/// <param name="EventDate">
/// 実施日
/// 未設定のときは null
/// 開始日から終了日の間に限る
/// </param>
/// <param name="OtherEventDates">
/// ほかの実施日。
/// 省くと無し。
/// 開始日から終了日の間に限る。
/// 種類が日程のときは使わない。
/// </param>
/// <param name="Kind">
/// ノートの種類。
/// 0 は通常のノートで、1 は日程。
/// 作ったあとに変えられない。
/// </param>
/// <param name="Schedule">
/// 日程。
/// 種類が日程のときだけ使い、省くと無し。
/// 日付は開始日から終了日の間に限る。
/// </param>
/// <remarks>新規の登録と編集で同じ</remarks>
public sealed record NoteSaveRequest(string Title, string Body, int Priority, bool IsPrivate, DateOnly StartDate, DateOnly? EndDate, DateOnly? EventDate, IReadOnlyList<DateOnly>? OtherEventDates = null, int Kind = 0, IReadOnlyList<ScheduleItemDto>? Schedule = null);

/// <summary>ノートを削除する・復元する要求</summary>
/// <param name="IsDeleted">削除するなら true、復元するなら false。</param>
public sealed record NoteDeletedRequest(bool IsDeleted);
