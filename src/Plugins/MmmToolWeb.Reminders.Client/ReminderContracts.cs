namespace MmmToolWeb.Reminders.Client;

/// <summary>「今日」に出すリマインダー 1 件</summary>
/// <param name="No">リマインダーの番号</param>
/// <param name="Time">
/// 鳴らす時刻
/// <c>HH:mm</c> の文字
/// </param>
/// <param name="Title">件名</param>
/// <param name="Note">
/// 備考
/// 無いときは null
/// </param>
/// <param name="LinkUrl">
/// 件名から開くリンク
/// <c>http</c> か <c>https</c> のときだけ
/// それ以外と無いときは null
/// </param>
/// <param name="Status">
/// その日の対応状態。
/// 0 は未、1 は完了、2 はスヌーズ。
/// Web 版では変えず、表示するだけ。
/// </param>
public sealed record ReminderTodayItemDto(int No, string Time, string Title, string? Note, string? LinkUrl, int Status);

/// <summary>ある日の対象</summary>
/// <param name="Items">その日に発動する、削除されていない、自分宛てと全員宛てのリマインダー。</param>
/// <remarks>時刻、番号の順に並んでいる。</remarks>
public sealed record ReminderTodayResponse(IReadOnlyList<ReminderTodayItemDto> Items);

/// <summary>「すべて」に出すリマインダー 1 件</summary>
/// <param name="No">リマインダーの番号</param>
/// <param name="WhenText">
/// 日付指定は <c>yyyy/MM/dd</c> の日付。
/// 曜日指定は「月火水」のような曜日で、7 つ全部と 1 つも無いときは「毎日」。
/// </param>
/// <param name="Time">
/// 鳴らす時刻
/// <c>HH:mm</c> の文字
/// </param>
/// <param name="Title">件名</param>
/// <param name="IsDeleted">削除済みか</param>
public sealed record ReminderListItemDto(int No, string WhenText, string Time, string Title, bool IsDeleted);

/// <summary>見えるリマインダーの一覧</summary>
/// <param name="Items">一覧</param>
/// <remarks>
/// 日付、時刻、番号の順に並んでいる。
/// 曜日指定は後ろ。
/// </remarks>
public sealed record ReminderListResponse(IReadOnlyList<ReminderListItemDto> Items);

/// <summary>編集のために読むリマインダー 1 件の中身</summary>
/// <param name="No">リマインダーの番号</param>
/// <param name="IsDeleted">削除済みか</param>
/// <param name="Date">
/// 日付指定の日付
/// 曜日指定のときは null
/// </param>
/// <param name="Weekdays">
/// 曜日指定の曜日。
/// 月が 1、火が 2、水が 4、木が 8、金が 16、土が 32、日が 64 の合計。
/// 0 は曜日を選んでいない。
/// </param>
/// <param name="Time">
/// 鳴らす時刻
/// <c>HH:mm</c> の文字
/// </param>
/// <param name="Title">件名</param>
/// <param name="Note">備考</param>
/// <param name="Link">リンク</param>
/// <param name="IsSpeak">PC で読み上げるか</param>
/// <param name="TargetUserId">
/// 宛先の人の番号
/// 0 は全員
/// </param>
/// <param name="ChannelIds">
/// 選んでいる送信先の番号
/// 自分が登録した送信先の分だけ
/// </param>
public sealed record ReminderDetailDto(
    int No,
    bool IsDeleted,
    DateOnly? Date,
    int Weekdays,
    string Time,
    string Title,
    string Note,
    string Link,
    bool IsSpeak,
    int TargetUserId,
    IReadOnlyList<int> ChannelIds);

/// <summary>保存する内容</summary>
/// <param name="Date">
/// 日付指定の日付
/// 曜日指定のときは null
/// </param>
/// <param name="Weekdays">
/// 曜日指定の曜日
/// <see cref="ReminderDetailDto.Weekdays"/> と同じ数え方
/// 日付指定のときは無視する
/// </param>
/// <param name="Time">
/// 鳴らす時刻
/// <c>HH:mm</c> の文字
/// </param>
/// <param name="Title">件名</param>
/// <param name="Note">備考</param>
/// <param name="Link">リンク</param>
/// <param name="IsSpeak">PC で読み上げるか</param>
/// <param name="TargetUserId">
/// 宛先の人の番号
/// 0 は全員
/// </param>
/// <param name="ChannelIds">選んだ送信先の番号</param>
/// <remarks>新規と編集で同じ</remarks>
public sealed record ReminderSaveRequest(
    DateOnly? Date,
    int Weekdays,
    string Time,
    string Title,
    string Note,
    string Link,
    bool IsSpeak,
    int TargetUserId,
    IReadOnlyList<int> ChannelIds);

/// <summary>保存の結果</summary>
/// <param name="No">
/// 保存したリマインダーの番号
/// 新規のときは採番した番号
/// </param>
public sealed record ReminderSaveResult(int No);

/// <summary>この端末が登録した送信先を探す要求</summary>
/// <param name="Endpoint">
/// この端末のブラウザが持つ、通知の宛先。
/// 秘密なので、道には載せず、本文で送る。
/// </param>
public sealed record ReminderDeviceRequest(string Endpoint);

/// <summary>この端末が登録した送信先</summary>
/// <param name="ChannelId">
/// 送信先の番号。
/// この端末が自分の送信先として登録していないとき、削除済みのときは 0。
/// </param>
public sealed record ReminderDeviceResponse(int ChannelId);

/// <summary>削除する・削除を取り消す</summary>
/// <param name="IsDeleted">削除するなら true</param>
public sealed record ReminderDeletedRequest(bool IsDeleted);

/// <summary>宛先に選べる人 1 人</summary>
/// <param name="UserId">
/// 人の番号
/// 0 は全員
/// </param>
/// <param name="Name">表示する名前</param>
public sealed record ReminderTargetOptionDto(int UserId, string Name);

/// <summary>送信先に選べる自分が登録した送信先 1 つ</summary>
/// <param name="Id">送信先の番号</param>
/// <param name="Label">
/// 登録名と種類
/// 例: <c>自分の iPhone (Web 版通知)</c>
/// </param>
/// <remarks>送る先の値は含めない</remarks>
public sealed record ReminderChannelOptionDto(int Id, string Label);

/// <summary>入力のシートが要る選択肢と文字数の上限</summary>
/// <param name="Targets">
/// 宛先に選べる人。
/// 先頭が自分、次が全員。
/// </param>
/// <param name="Channels">送信先に選べる、自分が登録した送信先。</param>
/// <param name="TitleMaxLength">件名の最大文字数</param>
/// <param name="NoteMaxLength">備考の最大文字数</param>
/// <param name="LinkMaxLength">リンクの最大文字数</param>
/// <remarks>
/// 文字数の上限は今の画面は使わない。
/// PC 版と共通の <c>ReminderLimits</c> を直接使う。
/// 上限の項目を残すのは更新の前に開いたままの画面が続けて動くため。
/// 古い画面は上限をこの値から読むので、項目が無いと 0 に読んで、入力できなくなる。
/// </remarks>
public sealed record ReminderOptionsResponse(
    IReadOnlyList<ReminderTargetOptionDto> Targets,
    IReadOnlyList<ReminderChannelOptionDto> Channels,
    int TitleMaxLength,
    int NoteMaxLength,
    int LinkMaxLength);
