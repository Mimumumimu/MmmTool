using System.Windows.Input;
using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>
/// 会話の中の 1 通。画面に出す文字と、並べる向き。
/// </summary>
/// <param name="source">メッセージ</param>
/// <param name="isMine">自分が送ったメッセージか</param>
/// <param name="openLinkCommand">本文のリンクを押したときのコマンド。引数は、開く URL の文字列</param>
/// <param name="readText">相手が読んだことを表す文字。1 対 1 は「既読」、グループは「既読 N」。読まれていない・自分が送ったのでないときは空文字</param>
/// <param name="showSender">送った人の名前を吹き出しの上に出すか。グループで、他の人が送ったメッセージのとき</param>
/// <param name="readerNames">読んだ人の名前。1 行に 1 人。グループで、読んだ人がいるときだけ。「既読 N」を押したときの一覧に出す</param>
/// <remarks>受け取ったメッセージは左、自分が送ったメッセージは右に寄せる。システムメッセージは、中央に小さな薄い文字で出す。向きと、出す・出さないは、画面が、この値から決める。</remarks>
public sealed class ChatMessageItem(
    ChatMessage source, bool isMine, ICommand openLinkCommand, string readText = "", bool showSender = false, string readerNames = "")
{
    /// <summary>相手が読んだことを表す文字。読まれていないときは空文字</summary>
    public string ReadText { get; } = isMine ? readText : "";

    /// <summary>読んだ人の名前。1 行に 1 人。グループで、読んだ人がいるときだけ。それ以外は空文字</summary>
    public string ReaderNames { get; } = isMine ? readerNames : "";

    /// <summary>「既読 N」を、押して名前の一覧を開けるボタンで出すか。グループで、読んだ人がいるとき</summary>
    public bool HasReaderNames => ReaderNames.Length > 0;

    /// <summary>「既読」を、押せない文字で出すか。1 対 1 で、読まれたとき</summary>
    public bool HasPlainReadText => ReadText.Length > 0 && !HasReaderNames;

    /// <summary>本文のリンクを押したときのコマンド。引数は、開く URL の文字列</summary>
    public ICommand OpenLinkCommand { get; } = openLinkCommand;

    /// <summary>元のメッセージ</summary>
    public ChatMessage Source { get; } = source;

    /// <summary>メッセージの番号</summary>
    public int Id { get; } = source.Id;

    /// <summary>システムメッセージ (人の追加・抜けた・名前の変更)か</summary>
    public bool IsSystem { get; } = source.IsSystem;

    /// <summary>自分が送ったメッセージか</summary>
    public bool IsMine { get; } = isMine;

    /// <summary>送った人の表示名。自分が送ったメッセージと、名前を出さないときは空文字</summary>
    public string SenderText { get; } = !isMine && showSender ? source.SenderName : "";

    /// <summary>本文</summary>
    public string Body { get; } = source.Body;

    /// <summary>送った日時の表示</summary>
    public string TimeText { get; } = MessageTimeText.Format(source.CreatedAt);

    /// <summary>返信元の引用。送った人の名前と本文の 1 行目。返信でない、または読めないときは空文字</summary>
    public string QuoteText { get; } = source is { ReplyToMessageId: > 0, ReplyToText: not null }
        ? $"{source.ReplyToSenderName}  {source.ReplyToText}"
        : "";

    /// <summary>送った人の名前を出すか</summary>
    public bool HasSender => SenderText.Length > 0;

    /// <summary>返信元の引用を出すか</summary>
    public bool HasQuote => QuoteText.Length > 0;

    /// <summary>相手が読んだことを表す文字を出すか</summary>
    public bool HasReadText => ReadText.Length > 0;
}
