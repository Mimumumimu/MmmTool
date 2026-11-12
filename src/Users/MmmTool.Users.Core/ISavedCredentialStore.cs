namespace MmmTool.Users.Core;

/// <summary>ログインに成功したログイン名とパスワードをこの PCに覚えておく</summary>
public interface ISavedCredentialStore
{
    /// <summary>覚えているログイン名とパスワードを取得する</summary>
    /// <returns>
    /// 覚えている内容
    /// 無ければ (読めないときも)null
    /// </returns>
    SavedCredential? Load();

    /// <summary>ログイン名とパスワードを覚える</summary>
    /// <param name="credential">覚える内容</param>
    /// <remarks>すでにあれば上書きする</remarks>
    void Save(SavedCredential credential);

    /// <summary>覚えているログイン名とパスワードを消す</summary>
    void Clear();
}

/// <summary>覚えておくログイン名とパスワード</summary>
/// <param name="LoginName">ログイン名</param>
/// <param name="Password">パスワード</param>
public sealed record SavedCredential(string LoginName, string Password);
