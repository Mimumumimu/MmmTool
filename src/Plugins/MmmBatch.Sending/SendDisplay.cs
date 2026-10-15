using MmmBatch.Sending.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.Sending;

/// <summary>送信の画面に出す文字列のうち、機能に共通のもの (機能ごとの状態の文字列は、各機能の画面に置く)</summary>
public static class SendDisplay
{
    /// <summary>送信先を、種類と登録名にして、画面に出す文字列にする</summary>
    /// <param name="kind">送信先の区分。送信先が無ければ null</param>
    /// <param name="name">送信先の登録名</param>
    /// <returns>「登録名 (ntfy)」「登録名 (Discord)」の形 (名前を先に、種類を補足として添える)。送信先が無ければ空文字</returns>
    /// <remarks>送る先の値 (トピック名・Webhook の URL)は秘密なので、出さない。</remarks>
    public static string ChannelText(NotificationChannelKind? kind, string name)
    {
        var kindText = kind switch
        {
            NotificationChannelKind.Ntfy => "ntfy",
            NotificationChannelKind.Discord => "Discord",
            _ => "",
        };
        if (name.Length == 0 && kindText.Length == 0)
        {
            return "";
        }

        return kindText.Length == 0 ? name : $"{name} ({kindText})";
    }

    /// <summary>宛先を、送信先を登録した人と、種類・登録名にして、画面に出す文字列にする</summary>
    /// <param name="ownerName">送信先を登録した人の表示名。分からなければ空文字</param>
    /// <param name="kind">送信先の区分</param>
    /// <param name="name">送信先の登録名</param>
    /// <returns>「ユーザー名 - 登録名 (ntfy)」の形 (MmmTool の宛先の選択肢と同じ)。ユーザー名が分からなければ「登録名 (ntfy)」</returns>
    /// <remarks>送る先の値は秘密なので、出さない。</remarks>
    public static string RecipientText(string ownerName, NotificationChannelKind kind, string name)
    {
        var channel = ChannelText(kind, name);
        return ownerName.Length == 0 ? channel : $"{ownerName} - {channel}";
    }

    /// <summary>送った 1 回の結果を、画面に出す文字列にする</summary>
    /// <param name="status">結果 (送信済み・失敗)</param>
    /// <param name="error">失敗の理由。成功は空文字</param>
    /// <returns>「成功」「失敗」。失敗は、理由があれば「失敗 (理由)」</returns>
    public static string ResultText(SendStatus status, string error) => status switch
    {
        SendStatus.Sent => "成功",
        SendStatus.Failed when error.Length == 0 => "失敗",
        SendStatus.Failed => $"失敗 ({error})",
        _ => status.ToString(),
    };
}
