namespace MmmTool.Data.Connection;

/// <summary>
/// DB へのログインの方式。
/// </summary>
/// <remarks>設定ストアには、名前 (<c>Sql</c> / <c>Windows</c>)で保存する。</remarks>
public enum DatabaseAuthentication
{
    /// <summary>ユーザー名とパスワード (既定)</summary>
    Sql = 0,

    /// <summary>今の Windows ユーザー</summary>
    Windows = 1,
}
