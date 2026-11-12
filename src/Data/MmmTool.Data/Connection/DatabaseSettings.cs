namespace MmmTool.Data.Connection;

/// <summary>DB への接続の設定でパスワードを除く</summary>
/// <remarks>
/// パスワードは設定ストアではなく、秘密の保管庫に保存する。
/// 保存するのは <see cref="DatabaseSettingsService"/>。
/// 値は作ったあとに書き換えず、<c>with</c> で新しく作る。
/// </remarks>
public sealed record DatabaseSettings
{
    /// <summary>保存先の種類</summary>
    public DatabaseMode Mode { get; init; }

    /// <summary>サーバー名</summary>
    public string Server { get; init; } = "";

    /// <summary>データベース名</summary>
    public string Name { get; init; } = "";

    /// <summary>ログインの方式</summary>
    public DatabaseAuthentication Authentication { get; init; }

    /// <summary>ユーザー名</summary>
    /// <remarks>ユーザー名とパスワードのログインのとき</remarks>
    public string UserName { get; init; } = "";

    /// <summary>サーバーの証明書を検証せずに信頼するか</summary>
    /// <remarks>自己署名の証明書のサーバーのとき</remarks>
    public bool TrustServerCertificate { get; init; }
}
