namespace MmmTool.Users.Core;

/// <summary>
/// 登録しようとしたログイン名が、すでに使われている。
/// </summary>
/// <remarks>利用者が別のログイン名に直せる、予測できる失敗 (バグではない)。</remarks>
public sealed class LoginNameTakenException : Exception
{
    /// <summary>例外を作る</summary>
    public LoginNameTakenException()
    {
    }

    /// <summary>内側の例外つきで例外を作る</summary>
    /// <param name="innerException">原因の例外 (保存先が出した、重複のエラー)</param>
    public LoginNameTakenException(Exception innerException)
        : base("ログイン名はすでに使われています。", innerException)
    {
    }
}
