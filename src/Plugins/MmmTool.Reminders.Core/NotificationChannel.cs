namespace MmmTool.Reminders.Core;

/// <summary>送信先 (ntfy・Discord)の登録 1 件</summary>
/// <remarks>
/// DB モードだけで使う (ローカルモードでは保存しない)。値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る (<c>init</c>)。
/// <see cref="Value"/> は秘密 (ntfy のトピック名・Discord の Webhook の URL)なので、画面の一覧に出さず、ログにも書かない。
/// </remarks>
public sealed record NotificationChannel
{
    /// <summary>番号 (<c>dbo.NotificationChannel.Id</c>)。0 は未採番 (新規)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>登録したユーザー (<c>AppUser.Id</c>)</summary>
    /// <remarks>DB が持つ値を読むだけ (保存先が、作成するときに今のユーザーを入れる。更新では変えない)。</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>区分</summary>
    public NotificationChannelKind Kind { get; init; }

    /// <summary>登録名 (画面に出す名前)</summary>
    public string Name { get; init; } = "";

    /// <summary>送る先の値 (ntfy はトピック名、Discord は Webhook の URL)。秘密</summary>
    public string Value { get; init; } = "";
}
