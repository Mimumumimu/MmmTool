namespace MmmTool.Messages.Core;

/// <summary>
/// 会話の参加者 1 人の、読んだ位置。
/// </summary>
/// <param name="UserId">参加者の番号 <c>AppUser.Id</c></param>
/// <param name="LastReadMessageId">その人が読んだ最後のメッセージの番号。0 は何も読んでいない</param>
/// <remarks>送った人の画面に出す「既読」の元になる。メッセージの番号が <paramref name="LastReadMessageId"/> 以下なら、その人は読んでいる。</remarks>
public sealed record ReadPosition(int UserId, int LastReadMessageId);
