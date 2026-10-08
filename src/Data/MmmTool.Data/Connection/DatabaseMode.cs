namespace MmmTool.Data.Connection;

/// <summary>
/// 保存先の種類。
/// </summary>
/// <remarks>設定ストアには、名前 (<c>Json</c> / <c>SqlServer</c>)で保存する。DB の種類を足すときは、ここに足す。</remarks>
public enum DatabaseMode
{
    /// <summary>JSON ファイル (既定)</summary>
    Json = 0,

    /// <summary>SQL Server</summary>
    SqlServer = 1,
}
