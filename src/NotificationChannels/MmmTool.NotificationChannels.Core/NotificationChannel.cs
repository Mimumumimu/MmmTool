namespace MmmTool.NotificationChannels.Core;

/// <summary>送信先の登録 1 件</summary>
/// <remarks>
/// DB モードだけで使い、ローカルモードでは保存しない。
/// 値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る。
/// 値は <c>init</c> で設定する。
/// <see cref="Value"/> は秘密 (ntfy のトピック名・Discord の Webhook の URL)なので、画面の一覧に出さず、ログにも書かない。
/// </remarks>
public sealed record NotificationChannel
{
    /// <summary>番号で、dbo.NotificationChannel.Id の値</summary>
    /// <remarks>0 は未採番で、新規を表す。</remarks>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>使用者のユーザーで、AppUser.Id の値</summary>
    /// <remarks>
    /// この送信先を使う人。
    /// DB が持つ値を読むだけ。
    /// 保存先が作成するときに登録した今のユーザーを入れ、更新では変えない。
    /// 一覧・選択・更新・削除の条件に使う業務の列で、DB の <c>UserId</c>。
    /// 共通ヘッダーの <c>CreatedByUserId</c> は条件に使わない。
    /// </remarks>
    public int UserId { get; init; }

    /// <summary>区分</summary>
    public NotificationChannelKind Kind { get; init; }

    /// <summary>登録名</summary>
    public string Name { get; init; } = "";

    /// <summary>送る先の値</summary>
    /// <remarks>
    /// ntfy はトピック名、Discord は Webhook の URL、Web 版通知は端末の宛先の JSON。
    /// 秘密。
    /// </remarks>
    public string Value { get; init; } = "";
}
