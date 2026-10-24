namespace MmmToolWeb.Notifications.Client;

/// <summary>
/// 通知を受け取る端末 1 件。
/// </summary>
/// <param name="Id">送信先の番号。</param>
/// <param name="Name">登録名。</param>
/// <remarks>端末の宛先 (配達係の URL と鍵)は秘密なので、持たない。</remarks>
public sealed record PushDeviceDto(int Id, string Name);

/// <summary>
/// 通知の画面を出すのに要る情報。
/// </summary>
/// <param name="PublicKey">送り手の公開鍵で、base64url の文字列。サーバーに鍵が設定されていなければ null。</param>
/// <param name="Devices">自分が登録した端末。</param>
public sealed record PushSettingsResponse(string? PublicKey, IReadOnlyList<PushDeviceDto> Devices);

/// <summary>
/// この端末の宛先を登録する要求。
/// </summary>
/// <param name="Id">この端末が覚えている送信先の番号。無ければ 0。あれば、その行を更新する。</param>
/// <param name="Name">登録名。</param>
/// <param name="Endpoint">配達係の宛先の URL。</param>
/// <param name="P256dh">通知を暗号化する公開鍵で、base64url の文字列。</param>
/// <param name="Auth">通知を暗号化する認証の秘密で、base64url の文字列。</param>
public sealed record PushRegisterRequest(int Id, string Name, string Endpoint, string P256dh, string Auth);

/// <summary>
/// 登録の結果。
/// </summary>
/// <param name="Id">登録した送信先の番号。</param>
public sealed record PushRegisterResponse(int Id);

/// <summary>
/// 受け取った通知 1 件。
/// </summary>
/// <param name="Key">通知の識別子。「種類:番号」の形で、通知を押したときに、この通知を開くために使う。</param>
/// <param name="KindName">種類の名前。</param>
/// <param name="Title">見出し。</param>
/// <param name="Body">本文。無ければ空文字。</param>
/// <param name="Link">リンク。無ければ null。</param>
/// <param name="ReceivedAt">届いた日時。未読かどうかを、端末の時計で比べるために添える。</param>
/// <param name="ReceivedAtText">届いた日時の表示。サーバーの時計で作る。</param>
public sealed record ReceivedNotificationDto(
    string Key, string KindName, string Title, string Body, string? Link, DateTimeOffset ReceivedAt, string ReceivedAtText);

/// <summary>
/// 受け取った通知の一覧。
/// </summary>
/// <param name="Items">新しい順の通知。</param>
public sealed record ReceivedListResponse(IReadOnlyList<ReceivedNotificationDto> Items);

/// <summary>
/// 通知を押したときに開く画面。
/// </summary>
/// <param name="Path">開く画面の道。先頭は「/」。行き先が無い種類・開けないときは null。</param>
public sealed record DestinationResponse(string? Path);

/// <summary>
/// 登録を解除する要求。
/// </summary>
/// <param name="Id">解除する送信先の番号。</param>
public sealed record PushRemoveRequest(int Id);
