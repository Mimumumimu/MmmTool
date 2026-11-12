namespace MmmToolWeb.Features.Client;

/// <summary>読んだ位置をサーバーが持つ機能の読んでいない数をサーバーに聞く口</summary>
/// <remarks>
/// 端末が数える通知 (ノートなど)は端末の控えの一覧から数える。
/// サーバーが数える機能はこれを登録して、ホーム画面のタイルの数を出す。
/// ホームの画面は機能の名前を持たず、<see cref="ClientFeature.UnreadKind"/> と <see cref="Kind"/> を突き合わせるだけ。
/// </remarks>
public interface IUnreadCountSource
{
    /// <summary>この口が数える通知の種類</summary>
    /// <remarks>ClientFeature.UnreadKind と同じ値</remarks>
    string Kind { get; }

    /// <summary>読んでいない数をサーバーに聞く</summary>
    /// <returns>
    /// 読んでいない数
    /// 聞けなかったときは null
    /// </returns>
    Task<int?> GetCountAsync();
}
