namespace MmmTool.Notes.Core;

/// <summary>
/// ノートの本文 (普通の文章に、HTML のタグが混ざったもの)を、表示してよい HTML にする。
/// </summary>
public interface INoteBodyFormatter
{
    /// <summary>本文を、安全な HTML の断片にする</summary>
    /// <param name="body">本文 (書いたとおりの文字)</param>
    /// <returns>表示してよい HTML の断片。許可したタグ以外は含まない</returns>
    string ToHtml(string body);
}
