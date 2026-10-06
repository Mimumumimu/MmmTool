namespace MmmTool.Data.Connection;

/// <summary>
/// DB への接続の設定が足りない・正しくないときの例外。メッセージはそのまま画面に出せる。
/// </summary>
/// <remarks>利用者の設定漏れ (サーバー名が空・パスワードが未登録など)で、バグではない。続けられる失敗なので、画面の InfoBar で知らせる。</remarks>
/// <param name="message">画面に出せるメッセージ</param>
public sealed class DatabaseSettingsException(string message) : Exception(message);
