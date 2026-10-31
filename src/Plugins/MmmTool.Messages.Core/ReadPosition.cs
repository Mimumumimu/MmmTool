namespace MmmTool.Messages.Core;

/// <summary>
/// 会話の参加者 1 人の、名前と読んだ位置。
/// </summary>
/// <param name="UserId">参加者の番号 <c>AppUser.Id</c></param>
/// <param name="LastReadMessageId">その人が読んだ最後のメッセージの番号。0 は何も読んでいない</param>
/// <param name="DisplayName">参加者の表示名。分からなければ空文字</param>
/// <remarks>
/// 送った人の画面に出す「既読」の元になる。メッセージの番号が <paramref name="LastReadMessageId"/> 以下なら、その人は読んでいる。
/// 名前は、グループの「既読 N」を押したときの名前の一覧と、参加者の画面の一覧に使う。
/// </remarks>
public sealed record ReadPosition(int UserId, int LastReadMessageId, string DisplayName);
