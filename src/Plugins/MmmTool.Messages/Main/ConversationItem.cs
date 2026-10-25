using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>
/// 人の一覧の 1 行。画面に出す文字。
/// </summary>
/// <param name="source">会話の要約</param>
/// <remarks>送る先の値は持たない。秘密のため。</remarks>
public sealed class ConversationItem(ConversationSummary source)
{
    /// <summary>元の会話の要約。読み直したときに、変わった行だけ差し替えるための比較にも使う</summary>
    public ConversationSummary Source { get; } = source;

    /// <summary>会話の番号</summary>
    public int Id { get; } = source.Id;

    /// <summary>会話の名前。1 対 1 は相手の表示名、グループは会話の名前</summary>
    public string Name { get; } = source.Name;

    /// <summary>最後のメッセージの 1 行目</summary>
    public string LastText { get; } = source.LastText;

    /// <summary>最後のメッセージの日時。今日なら「HH:mm」、それ以外は「M/d HH:mm」</summary>
    public string TimeText { get; } = MessageTimeText.Format(source.LastAt);

    /// <summary>相手が受け取り先を指定していないときの案内。指定しているときは空文字</summary>
    public string NoteText { get; } = source.PeerCanReceive ? "" : MessageDraft.NoReceiveTargetMessage;

    /// <summary>案内を出すか</summary>
    public bool HasNote => NoteText.Length > 0;
}
