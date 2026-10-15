namespace MmmBatch.Sending.Core;

/// <summary>送り直しの決まり (機能に共通)</summary>
/// <remarks>
/// 送り先がスマホの通知・Discord なので、少し遅れて届いても困らない。待つ時間を空けて、上限の回数まで送り直す。
/// 送り直しの前に送信が済んでいたときは、二重に送られうる (取りこぼすより、重複を許す側に倒す)。
/// </remarks>
public static class SendRetryPolicy
{
    /// <summary>1 つを送ろうとする回数の上限 (最初の 1 回を含む)</summary>
    public const int MaxAttempts = 5;

    /// <summary>送信中のまま、送り直しの対象にするまでの時間 (送っている途中で落ちたとみなす)</summary>
    public static readonly TimeSpan StalePendingAge = TimeSpan.FromMinutes(10);

    /// <summary>失敗してから、次に送り直すまでの待ち時間</summary>
    /// <param name="attempts">ここまでに送ろうとした回数</param>
    /// <returns>回数 × 1 分</returns>
    public static TimeSpan FailedWait(int attempts) => TimeSpan.FromMinutes(attempts);
}
