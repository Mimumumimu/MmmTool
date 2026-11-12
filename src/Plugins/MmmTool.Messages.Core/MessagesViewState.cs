namespace MmmTool.Messages.Core;

/// <summary>メッセージのページで、画面で選んだ状態</summary>
/// <remarks>
/// ページを作り直しても引き継ぐためにページの外で持つ。
/// ページは別のウィンドウに外す・戻す・機能をオフにしてオンに戻すときに作り直される。
/// そのとき ViewModel はここから読んで、開いていた会話・人を選ぶ画面・入力中の本文・返信先を元に戻し、画面で変えるたびにここへ書く。
/// アプリが動いている間だけ持ち、ファイルには保存しない。
/// 会話のメッセージ・会話の一覧・読み込みのエラー・送信中の表示は持たない。
/// 作り直したあとに保存先から読み直す。
/// </remarks>
public sealed class MessagesViewState
{
    /// <summary>会話を開いているか</summary>
    public bool IsConversationOpen { get; set; }

    /// <summary>開いている会話の番号</summary>
    public int ConversationId { get; set; }

    /// <summary>空の会話の画面の 1 対 1 の相手の番号</summary>
    public int DraftPeerUserId { get; set; }

    /// <summary>空のグループの画面のグループの名前</summary>
    public string DraftGroupName { get; set; } = "";

    /// <summary>空のグループの画面の参加者の番号</summary>
    /// <remarks>自分は含めない</remarks>
    public List<int> DraftGroupMemberIds { get; set; } = [];

    /// <summary>開いている会話の名前</summary>
    public string ConversationTitle { get; set; } = "";

    /// <summary>相手が通知先を指定しているか</summary>
    public bool PeerNotifiable { get; set; } = true;

    /// <summary>宛先の人を選ぶ画面にしているか</summary>
    public bool IsPickingPerson { get; set; }

    /// <summary>入力中の本文</summary>
    public string Body { get; set; } = "";

    /// <summary>返信の元にするメッセージの番号</summary>
    public int ReplyToId { get; set; }
}
