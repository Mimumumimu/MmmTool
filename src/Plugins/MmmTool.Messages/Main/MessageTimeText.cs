using System.Globalization;

namespace MmmTool.Messages.Main;

/// <summary>メッセージの日時を、画面に出す文字にする</summary>
public static class MessageTimeText
{
    /// <summary>日時を、表示の文字にする</summary>
    /// <param name="at">日時</param>
    /// <returns>今日なら「HH:mm」、それ以外は「M/d HH:mm」。ローカル時刻</returns>
    public static string Format(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        return local.Date == DateTime.Today ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("M/d HH:mm", CultureInfo.InvariantCulture);
    }
}
