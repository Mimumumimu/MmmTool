namespace MmmBatch.Sending.Core;

/// <summary>気づいてほしい失敗の窓口 (各機能の仕事が報告し、ホストがトレイの通知にする)</summary>
/// <remarks>ホストは、機能ごとの通知の購読を持たず、この窓口だけを購読する。</remarks>
public sealed class BatchAlertService
{
    /// <summary>気づいてほしい失敗が起きた</summary>
    /// <remarks>引数は、画面に出せるメッセージ。任意のスレッドから発火する。</remarks>
    public event EventHandler<string>? AlertRaised;

    /// <summary>気づいてほしい失敗を報告する</summary>
    /// <param name="message">画面に出せるメッセージ (送る先の値を含めない)</param>
    public void Raise(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        AlertRaised?.Invoke(this, message);
    }
}
