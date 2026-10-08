using MmmTool.Reminders.Core;

namespace MmmTool.Data.Reminders;

/// <summary>
/// DB の <c>dbo.NotificationChannel</c> の 1 行 (列と 1 対 1)。アプリの型 <see cref="NotificationChannel"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>変換は <see cref="FromChannel"/> と <see cref="ToChannel"/>。NULL は使わない (理由は docs/specs/database.md)。</remarks>
public sealed record NotificationChannelRow
{
    /// <summary>主キー (<see cref="NotificationChannel.Id"/>)。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    /// <remarks>DB の既定値で入る。<see cref="FromChannel"/> では決めない。</remarks>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (<c>AppUser.Id</c>)。この送信先を登録した人</summary>
    /// <remarks>保存先が、今のユーザーを入れる。<see cref="FromChannel"/> では決めない。</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    /// <remarks>更新の SQL の中で、DB サーバーの時計を使って書く。<see cref="FromChannel"/> では決めない。</remarks>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<c>AppUser.Id</c>)</summary>
    /// <remarks>保存先が、今のユーザーを入れる。<see cref="FromChannel"/> では決めない。</remarks>
    public int UpdatedByUserId { get; init; }

    /// <summary>区分 (ntfy = 1・Discord = 2)</summary>
    public byte Kind { get; init; }

    /// <summary>登録名</summary>
    public string Name { get; init; } = "";

    /// <summary>送る先の値 (トピック名 / Webhook の URL)</summary>
    public string Value { get; init; } = "";

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="channel">送信先</param>
    /// <returns>DB の行。作成・更新の日時と、作成者・更新者は決めない</returns>
    /// <exception cref="ArgumentException">区分が正しくない、登録名・値が空か長すぎる (長さの上限は <see cref="NotificationChannelRules"/>)。</exception>
    public static NotificationChannelRow FromChannel(NotificationChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!Enum.IsDefined(channel.Kind))
        {
            throw new ArgumentException($"送信先の区分が正しくありません ({(int)channel.Kind})。", nameof(channel));
        }
        if (string.IsNullOrWhiteSpace(channel.Name) || channel.Name.Length > NotificationChannelRules.NameMaxLength)
        {
            throw new ArgumentException($"登録名は、空でない {NotificationChannelRules.NameMaxLength} 文字までにしてください。", nameof(channel));
        }
        if (string.IsNullOrWhiteSpace(channel.Value) || channel.Value.Length > NotificationChannelRules.ValueMaxLength)
        {
            throw new ArgumentException($"送る先の値は、空でない {NotificationChannelRules.ValueMaxLength} 文字までにしてください。", nameof(channel));
        }

        return new NotificationChannelRow
        {
            Id = channel.Id,
            IsDeleted = channel.IsDeleted,
            Kind = (byte)channel.Kind,
            Name = channel.Name,
            Value = channel.Value,
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>送信先</returns>
    /// <exception cref="InvalidOperationException">区分が、定義にない値 (バグか、手で直した値)。</exception>
    public NotificationChannel ToChannel()
    {
        var kind = (NotificationChannelKind)Kind;
        if (!Enum.IsDefined(kind))
        {
            throw new InvalidOperationException($"送信先 {Id} の区分が正しくありません ({Kind})。");
        }

        return new NotificationChannel
        {
            Id = Id,
            IsDeleted = IsDeleted,
            CreatedByUserId = CreatedByUserId,
            Kind = kind,
            Name = Name,
            Value = Value,
        };
    }
}
