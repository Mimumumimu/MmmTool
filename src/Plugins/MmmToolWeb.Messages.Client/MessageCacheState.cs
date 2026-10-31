namespace MmmToolWeb.Messages.Client;

/// <summary>
/// 端末の控えに残す、人の一覧。ログインした人ごとに分けて持つ。
/// </summary>
/// <param name="UserId">控えの持ち主の番号。ほかの人でログインしたときは、捨てる。</param>
/// <param name="NextBase">次の差分の取得に送る基準の日時。</param>
/// <param name="Conversations">会話の要約。最後のメッセージが新しい順。</param>
/// <param name="Version">控えの形の版。欄を足したときに上げる。版の欄が無い古い控えは 0 になり、<see cref="CurrentVersion"/> と違うので、使わずに捨てる。</param>
/// <remarks>
/// 控えは、いつでも作り直せる。サーバーが唯一の正。
/// 差分の取得は、変わった会話の要約しか返さない。古い形の控えを使い続けると、足した欄が空のまま直らないので、版が違う控えは使わず、サーバーから読み直す。
/// </remarks>
public sealed record MessageCacheState(int UserId, DateTimeOffset NextBase, IReadOnlyList<ConversationDto> Conversations, int Version)
{
    /// <summary>いまの控えの形の版。2 は、会話の要約に、読んでいない数と読んだ位置を足した形。</summary>
    public const int CurrentVersion = 2;

    /// <summary>いまの版の控えを作る。</summary>
    /// <param name="userId">控えの持ち主の番号。</param>
    /// <param name="nextBase">次の差分の取得に送る基準の日時。</param>
    /// <param name="conversations">会話の要約。</param>
    /// <returns>いまの版の控え。</returns>
    public static MessageCacheState Create(int userId, DateTimeOffset nextBase, IReadOnlyList<ConversationDto> conversations)
        => new(userId, nextBase, conversations, CurrentVersion);
}

/// <summary>
/// 端末の控えに残す、会話 1 つのメッセージ。新しいほうから切れ目なく、上限の件数まで持つ。
/// </summary>
/// <param name="UserId">控えの持ち主の番号。</param>
/// <param name="ConversationId">会話の番号。</param>
/// <param name="Messages">メッセージ。古いものが先で、新しいものが後。</param>
public sealed record ConversationCacheState(int UserId, int ConversationId, IReadOnlyList<MessageDto> Messages);
