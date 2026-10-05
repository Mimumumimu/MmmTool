namespace MmmTool.Core.ClipboardTransfer;

/// <summary>ファイルをクリップボード用の JSON にした結果</summary>
/// <param name="Json">JSON 文字列。失敗したときは null</param>
/// <param name="FileNames">JSON にしたファイルの名前</param>
/// <param name="TotalBytes">JSON にしたファイルの合計の大きさ (バイト)</param>
/// <param name="Error">失敗の理由 (画面に出す文)。成功したときは null</param>
public sealed record TransferEncodeResult(string? Json, IReadOnlyList<string> FileNames, long TotalBytes, string? Error);

/// <summary>クリップボードの JSON を転送データに戻した結果</summary>
/// <param name="Files">転送データ。失敗したときは空</param>
/// <param name="Error">失敗の理由 (画面に出す文)。成功したときは null</param>
public sealed record TransferDecodeResult(IReadOnlyList<TransferFile> Files, string? Error);
