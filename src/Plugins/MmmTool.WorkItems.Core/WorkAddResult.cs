namespace MmmTool.WorkItems.Core;

/// <summary>行を追加した結果</summary>
/// <param name="Key">追加した行の識別。追加しなかったときは null</param>
/// <param name="Error">追加しなかった理由 (画面に出せるメッセージ)。追加したときは null</param>
public sealed record WorkAddResult(WorkKey? Key, string? Error);
