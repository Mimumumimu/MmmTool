namespace MmmTool.Data.Connection;

/// <summary>
/// DB への接続の設定 (パスワードを除く)。
/// </summary>
/// <remarks>パスワードは設定ストアではなく、秘密の保管庫に保存する (<see cref="DatabaseSettingsService"/>)。値は作ったあとに書き換えず、<c>with</c> で新しく作る。</remarks>
public sealed record DatabaseSettings
{
    /// <summary>保存先の種類 (既定は JSON)</summary>
    public DatabaseMode Mode { get; init; }

    /// <summary>サーバー名 (<c>ホスト名</c>・<c>ホスト名\インスタンス名</c>・<c>ホスト名,ポート</c>)</summary>
    public string Server { get; init; } = "";

    /// <summary>データベース名</summary>
    public string Name { get; init; } = "";

    /// <summary>ログインの方式 (既定はユーザー名とパスワード)</summary>
    public DatabaseAuthentication Authentication { get; init; }

    /// <summary>ユーザー名 (ユーザー名とパスワードのログインのとき)</summary>
    public string UserName { get; init; } = "";

    /// <summary>サーバーの証明書を検証せずに信頼するか (自己署名の証明書のサーバーのとき)</summary>
    public bool TrustServerCertificate { get; init; }
}
