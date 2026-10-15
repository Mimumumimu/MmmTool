using MmmTool.Messages.Core;

namespace MmmTool.Data.Messages;

/// <summary>
/// DB の <c>dbo.MessageSendRequest</c> の 1 行 (列と 1 対 1)。アプリの型 <see cref="MessageSendRequest"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>変換は <see cref="Validate"/> と <see cref="ToRequest"/>。NULL は使わない (理由は docs/specs/database.md)。</remarks>
public sealed record MessageSendRequestRow
{
    /// <summary>主キー (<see cref="MessageSendRequest.Id"/>)。DB が採番する</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか。常に false (共通ヘッダーのための列)</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時 (依頼した日時)</summary>
    /// <remarks>DB の既定値で入る。</remarks>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (<c>AppUser.Id</c>)。送った人と同じ</summary>
    /// <remarks>記録用の列。条件・結合・索引には使わない。</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    /// <remarks>更新の SQL の中で、DB サーバーの時計を使って書く。</remarks>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<c>AppUser.Id</c>)。依頼したときは送った人、MmmBatch が更新するときは 0</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>送った人 (<c>AppUser.Id</c>。業務の列 <c>SenderUserId</c>)</summary>
    public int SenderUserId { get; init; }

    /// <summary>送る先 (<c>NotificationChannel.Id</c>)</summary>
    public int ChannelId { get; init; }

    /// <summary>タイトル。空文字ならタイトルなし</summary>
    public string Title { get; init; } = "";

    /// <summary>本文</summary>
    public string Body { get; init; } = "";

    /// <summary>状態 (送信待ち・送信中 = 1・送信済み = 2・失敗 = 3)</summary>
    public byte Status { get; init; }

    /// <summary>試した回数</summary>
    public byte Attempts { get; init; }

    /// <summary>送った日時。未送信は <c>9999-12-31</c></summary>
    public DateTimeOffset SentAt { get; init; }

    /// <summary>足そうとする内容が正しいか調べる</summary>
    /// <param name="channelId">送る先の番号</param>
    /// <param name="title">タイトル</param>
    /// <param name="body">本文</param>
    /// <exception cref="ArgumentException">送る先の番号が正しくない・タイトルが長すぎる・本文が空か長すぎる (長さの上限は <see cref="MessageLimits"/>)。</exception>
    public static void Validate(int channelId, string title, string body)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        if (channelId <= 0)
        {
            throw new ArgumentException($"送る先の番号が正しくありません ({channelId})。", nameof(channelId));
        }
        if (title.Length > MessageLimits.TitleMaxLength)
        {
            throw new ArgumentException($"タイトルは {MessageLimits.TitleMaxLength} 文字までにしてください。", nameof(title));
        }
        if (string.IsNullOrWhiteSpace(body) || body.Length > MessageLimits.BodyMaxLength)
        {
            throw new ArgumentException($"本文は、空でない {MessageLimits.BodyMaxLength} 文字までにしてください。", nameof(body));
        }
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>送信依頼</returns>
    /// <exception cref="InvalidOperationException">状態が、定義にない値 (バグか、手で直した値)。</exception>
    public MessageSendRequest ToRequest()
    {
        var status = (MessageSendStatus)Status;
        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException($"メッセージの送信依頼 {Id} の状態が正しくありません ({Status})。");
        }

        return new MessageSendRequest(Id, SenderUserId, ChannelId, Title, Body, status, Attempts, CreatedAt, UpdatedAt);
    }
}
