namespace MmmTool.Backlog.Core;

/// <summary>
/// URL を場所に分けた結果
/// </summary>
/// <param name="Location">分けた場所。失敗したときは null</param>
/// <param name="Error">失敗の理由 (画面に出す文)。成功したときは null</param>
public sealed record BacklogLocationResult(BacklogLocation? Location, string? Error)
{
    /// <summary>成功の結果を作る</summary>
    /// <param name="location">分けた場所</param>
    /// <returns>成功の結果</returns>
    public static BacklogLocationResult Success(BacklogLocation location) => new(location, null);

    /// <summary>失敗の結果を作る</summary>
    /// <param name="error">失敗の理由 (画面に出す文)</param>
    /// <returns>失敗の結果</returns>
    public static BacklogLocationResult Failure(string error) => new(null, error);
}
