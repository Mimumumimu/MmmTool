namespace MmmTool.Messages.Core;

/// <summary>
/// 会話とメッセージの、読み込みの結果。最初の読み込みと、差分の取得で共通。
/// </summary>
/// <param name="Messages">変更のあったメッセージ。最初の読み込みでは空</param>
/// <param name="Conversations">変更のあった会話の要約。最初の読み込みでは、自分の会話すべて</param>
/// <param name="NextBase">次の差分の取得の基準にする日時。DB の時計で、読み始めたときの日時</param>
/// <remarks>端末は、次の差分を、<see cref="NextBase"/> から <see cref="MessageSync.Lookback"/> だけ戻した日時より後で取る。</remarks>
public sealed record MessageSyncResult(IReadOnlyList<ChatMessage> Messages, IReadOnlyList<ConversationSummary> Conversations, DateTimeOffset NextBase);
