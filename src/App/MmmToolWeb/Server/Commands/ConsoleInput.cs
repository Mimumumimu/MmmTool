using System.Text;

namespace MmmToolWeb.Server.Commands;

/// <summary>
/// サーバーの PC のコンソールで行う入力。
/// </summary>
internal static class ConsoleInput
{
    /// <summary>
    /// 文字列を聞く。何も入力されなければ、既定値を使う。
    /// </summary>
    /// <param name="label">聞く項目の名前。</param>
    /// <param name="defaultValue">何も入力されなかったときの値。</param>
    /// <returns>入力された値 (前後の空白を除く)。</returns>
    public static string Ask(string label, string defaultValue)
    {
        Console.Write(defaultValue.Length > 0 ? $"{label} [{defaultValue}]: " : $"{label}: ");
        var line = Console.ReadLine()?.Trim() ?? string.Empty;
        return line.Length > 0 ? line : defaultValue;
    }

    /// <summary>
    /// はい・いいえを聞く。何も入力されなければ、既定値を使う。
    /// </summary>
    /// <param name="label">聞く内容。</param>
    /// <param name="defaultValue">何も入力されなかったときの値。</param>
    /// <returns>「はい」なら <c>true</c>。</returns>
    public static bool AskYesNo(string label, bool defaultValue)
    {
        Console.Write($"{label} (y/n) [{(defaultValue ? "y" : "n")}]: ");
        var line = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;
        return line.Length == 0 ? defaultValue : line is "y" or "yes";
    }

    /// <summary>
    /// パスワードを、画面に表示せずに聞く。
    /// </summary>
    /// <param name="hasSaved">すでに保存されたパスワードがあるか (あれば、何も入力しないと、それを使う)。</param>
    /// <returns>入力されたパスワード。何も入力されなければ空文字。</returns>
    public static string AskPassword(bool hasSaved)
    {
        Console.Write(hasSaved ? "パスワード (変えないときは何も入力しない): " : "パスワード: ");
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
