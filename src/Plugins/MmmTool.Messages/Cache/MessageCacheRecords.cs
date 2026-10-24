using MmmTool.Messages.Core;

namespace MmmTool.Messages.Cache;

/// <summary>
/// 端末の控えに残す、人の一覧。ログインした人ごとに分けて持つ。
/// </summary>
/// <param name="UserId">控えの持ち主の番号。ほかの人でログインしたときは、捨てる</param>
/// <param name="NextBase">次の差分の取得の基準にする日時</param>
/// <param name="Conversations">会話の要約。最後のメッセージが新しい順</param>
/// <remarks>控えは、いつでも作り直せる。サーバーが唯一の正。</remarks>
public sealed record CachedConversationList(int UserId, DateTimeOffset NextBase, IReadOnlyList<ConversationSummary> Conversations);

/// <summary>
/// 端末の控えに残す、会話 1 つのメッセージ。新しいほうから切れ目なく、上限の件数まで持つ。
/// </summary>
/// <param name="UserId">控えの持ち主の番号</param>
/// <param name="Messages">メッセージ。古いものが先で、新しいものが後</param>
public sealed record CachedMessages(int UserId, IReadOnlyList<ChatMessage> Messages);
