namespace MmmTool.Messages.Core;

/// <summary>メッセージの、差分の取得の決まり</summary>
public static class MessageSync
{
    /// <summary>差分を取るとき、基準の日時から戻す時間。仮</summary>
    /// <remarks>
    /// 確定が遅れた書き込みの取りこぼしを避けるため。作業管理の 60 秒より短いのは、確認の間隔が数秒で、60 秒戻すと、毎回、読み直す行が増えすぎるため。
    /// メッセージの書き込みは短いので、この時間で確定が間に合う。
    /// </remarks>
    public static readonly TimeSpan Lookback = TimeSpan.FromSeconds(10);

    /// <summary>会話で、一度に読むメッセージの件数</summary>
    public const int PageSize = 50;

    /// <summary>端末の控えに残す、会話ごとのメッセージの件数。仮</summary>
    public const int CacheCountPerConversation = 200;

    /// <summary>基準の日時から、次の差分を取る日時を求める</summary>
    /// <param name="baseTime">前回の結果の次の基準の日時</param>
    /// <returns>この日時より後に更新された行を読む</returns>
    public static DateTimeOffset SinceOf(DateTimeOffset baseTime) => baseTime - Lookback;
}
