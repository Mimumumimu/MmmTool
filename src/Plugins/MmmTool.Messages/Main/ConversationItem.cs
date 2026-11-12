using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>人の一覧の 1 行</summary>
/// <param name="source">会話の要約</param>
/// <param name="myUserId">
/// 自分の番号
/// 自分が送った最後のメッセージの名前を「自分」にするために使う
/// </param>
/// <remarks>
/// 画面に出す文字
/// 送る先の値は持たない
/// 秘密のため
/// </remarks>
public sealed class ConversationItem(ConversationSummary source, int myUserId)
{
    /// <summary>元の会話の要約</summary>
    /// <remarks>読み直したときに変わった行だけ差し替えるための比較にも使う</remarks>
    public ConversationSummary Source { get; } = source;

    /// <summary>会話の番号</summary>
    public int Id { get; } = source.Id;

    /// <summary>会話の名前</summary>
    /// <remarks>1 対 1 は相手の表示名、グループは会話の名前。</remarks>
    public string Name { get; } = source.Name;

    /// <summary>最後のメッセージの 1 行目</summary>
    /// <remarks>グループは「送った人の名前: 本文」で、システムメッセージは本文だけ。</remarks>
    public string LastText { get; } = source.Kind == ConversationKind.Group && source.LastSenderUserId > 0
        ? $"{(source.LastSenderUserId == myUserId ? "自分" : source.LastSenderName)}: {source.LastText}"
        : source.LastText;

    /// <summary>最後のメッセージの日時</summary>
    /// <remarks>今日なら「HH:mm」、それ以外は「M/d HH:mm」。</remarks>
    public string TimeText { get; } = MessageTimeText.Format(source.LastAt);

    /// <summary>相手が通知先を指定していないときの案内</summary>
    /// <remarks>指定しているときは空文字</remarks>
    public string NoteText { get; } = source.PeerNotifiable ? "" : MessageDraft.PeerNoNotifyListText;

    /// <summary>案内を出すか</summary>
    public bool HasNote => NoteText.Length > 0;

    /// <summary>読んでいないメッセージがあるか</summary>
    /// <remarks>
    /// 青い点と名前の太字に使う。
    /// 読んだ位置は DB にあり、この PC の状態ではない。
    /// </remarks>
    public bool IsUnread { get; } = source.UnreadCount > 0;
}
