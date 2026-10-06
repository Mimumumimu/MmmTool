using System.Buffers.Text;
using System.Text.Json;
using MmmTool.ClipboardTransfer.Core.Json;

namespace MmmTool.ClipboardTransfer.Core;

/// <summary>
/// ファイルとクリップボード用の JSON を相互に変換し、ファイルとして保存する。
/// </summary>
/// <remarks>
/// 大きなファイルでも画面が止まらないよう、読み込み・変換・書き込みはスレッドプールで行う。
/// 予測できる失敗 (大きすぎる・JSON が正しくない)は、例外ではなく結果の <c>Error</c> で返す。
/// ファイルを読めない・書けない (ロック・権限など)は、呼び出し側が <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/> で受ける。
/// </remarks>
public sealed class ClipboardTransferService
{
    /// <summary>1 ファイルあたりの上限 (バイト)</summary>
    /// <remarks>Base64 にすると約 4/3 倍になり、文字列としてクリップボードに載るため、上限を設ける。</remarks>
    public const long MaxFileBytes = 160L * 1024 * 1024;

    /// <summary>ファイルを、クリップボード用の JSON にする</summary>
    /// <param name="paths">ファイルのパス (存在しないもの・フォルダーは対象にしない)</param>
    /// <returns>JSON にした結果</returns>
    /// <exception cref="IOException">ファイルを読めなかった。</exception>
    /// <exception cref="UnauthorizedAccessException">ファイルを読む権限が無かった。</exception>
    public Task<TransferEncodeResult> EncodeAsync(IReadOnlyList<string> paths) => Task.Run(() => Encode(paths));

    /// <summary>クリップボードの JSON を、転送データに戻す</summary>
    /// <param name="json">クリップボードのテキスト</param>
    /// <returns>転送データに戻した結果</returns>
    /// <remarks>ファイル名・Base64 が正しいかまで確かめる (保存の途中で壊れたファイルが混ざらないよう、保存の前に全件を確かめる)。</remarks>
    public Task<TransferDecodeResult> DecodeAsync(string json) => Task.Run(() => Decode(json));

    /// <summary>保存先のパスを返す</summary>
    /// <param name="folder">保存先のフォルダー</param>
    /// <param name="file">保存する転送データ (<see cref="DecodeAsync"/> で確かめたもの)</param>
    /// <returns>フォルダーの中の、そのファイルのパス</returns>
    public static string GetDestinationPath(string folder, TransferFile file) => Path.Combine(folder, file.FileName);

    /// <summary>保存先のフォルダーに、同じ名前のファイルが既にある転送データを返す</summary>
    /// <param name="folder">保存先のフォルダー</param>
    /// <param name="files">保存する転送データ</param>
    /// <returns>同じ名前のファイルが既にあるもの (無ければ空)</returns>
    public static IReadOnlyList<TransferFile> FindConflicts(string folder, IReadOnlyList<TransferFile> files) =>
        files.Where(file => File.Exists(GetDestinationPath(folder, file))).ToList();

    /// <summary>転送データ 1 件を、ファイルとして保存する</summary>
    /// <param name="file">保存する転送データ (<see cref="DecodeAsync"/> で確かめたもの)</param>
    /// <param name="destinationPath">保存先のパス (同じ名前のファイルがあれば上書きする)</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="IOException">ファイルを書けなかった。</exception>
    /// <exception cref="UnauthorizedAccessException">ファイルを書く権限が無かった。</exception>
    /// <remarks>作成日時・更新日時が有効なら、保存のあとに元の値へ戻す。</remarks>
    public Task SaveAsync(TransferFile file, string destinationPath) => Task.Run(() =>
    {
        File.WriteAllBytes(destinationPath, Convert.FromBase64String(file.Base64Data));
        if (IsValidTimestamp(file.CreationTime))
        {
            File.SetCreationTime(destinationPath, file.CreationTime.LocalDateTime);
        }
        if (IsValidTimestamp(file.LastWriteTime))
        {
            File.SetLastWriteTime(destinationPath, file.LastWriteTime.LocalDateTime);
        }
    });

    /// <summary>ファイルを、クリップボード用の JSON にする</summary>
    /// <param name="paths">ファイルのパス</param>
    /// <returns>JSON にした結果</returns>
    private static TransferEncodeResult Encode(IReadOnlyList<string> paths)
    {
        var files = paths.Where(File.Exists).Select(path => new FileInfo(path)).ToList();
        if (files.Count == 0)
        {
            return new(null, [], 0, "送れるファイルがありません。");
        }

        if (FindDuplicateName(files.Select(file => file.Name)) is { } duplicate)
        {
            return new(null, [], 0, DuplicateMessage(duplicate));
        }

        // 1 つでも大きすぎれば、どれも読み込まずに止める (読んでから失敗すると、時間が無駄になる)
        var tooLarge = files.Where(file => file.Length > MaxFileBytes).Select(file => file.Name).ToList();
        if (tooLarge.Count > 0)
        {
            return new(null, [], 0, $"{string.Join("、", tooLarge)} が大きすぎます。1 ファイル {MaxFileBytes / 1024 / 1024} MB までです。");
        }

        var transfers = files
            .Select(file => new TransferFile(
                file.Name,
                Convert.ToBase64String(File.ReadAllBytes(file.FullName)),
                new DateTimeOffset(file.CreationTime),
                new DateTimeOffset(file.LastWriteTime)))
            .ToList();
        var json = JsonSerializer.Serialize(transfers, TransferJsonContext.Default.ListTransferFile);
        return new(json, files.ConvertAll(file => file.Name), files.Sum(file => file.Length), null);
    }

    /// <summary>クリップボードの JSON を、転送データに戻す</summary>
    /// <param name="json">クリップボードのテキスト</param>
    /// <returns>転送データに戻した結果</returns>
    private static TransferDecodeResult Decode(string json)
    {
        const string NotTransferData = "クリップボードの内容は、転送データではありません。";

        List<TransferFile>? files;
        try
        {
            files = JsonSerializer.Deserialize(json, TransferJsonContext.Default.ListTransferFile);
        }
        catch (JsonException)
        {
            // 任意のテキストが入っていることが多いので、先に確かめる方法は無い
            return new([], NotTransferData);
        }

        if (files is not { Count: > 0 })
        {
            return new([], NotTransferData);
        }

        foreach (var file in files)
        {
            if (file is null || !IsSafeFileName(file.FileName))
            {
                return new([], $"ファイル名が正しくありません ({file?.FileName})。");
            }
            if (file.Base64Data is null || !Base64.IsValid(file.Base64Data))
            {
                return new([], $"{file.FileName} の内容が正しくありません。");
            }
        }

        // 同じ名前が 2 つあると、あとの分が先の分を黙って上書きしてしまう
        if (FindDuplicateName(files.Select(file => file.FileName)) is { } duplicate)
        {
            return new([], DuplicateMessage(duplicate));
        }

        return new(files, null);
    }

    /// <summary>同じ名前が複数あるときの、その名前を返す</summary>
    /// <param name="names">ファイル名</param>
    /// <returns>重複した名前。無ければ null</returns>
    /// <remarks>Windows のファイル名は大文字小文字を区別しないので、区別せずに比べる。</remarks>
    private static string? FindDuplicateName(IEnumerable<string> names) =>
        names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1)?.Key;

    /// <summary>同じ名前のファイルがあるときのエラーメッセージ</summary>
    /// <param name="name">重複した名前</param>
    /// <returns>画面に出すメッセージ</returns>
    private static string DuplicateMessage(string name) => $"同じ名前のファイルは、一度に送れません ({name})。";

    /// <summary>ファイル名が、保存先のフォルダーの中にだけ書けるものか</summary>
    /// <param name="fileName">確かめるファイル名</param>
    /// <returns>フォルダーの区切り・使えない文字を含まない、ファイル名なら true</returns>
    /// <remarks>送られてきた名前をそのまま使うと、フォルダーの外 (「..\」など)へ書けてしまうため。</remarks>
    private static bool IsSafeFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName is not ("." or "..")
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>日時が、ファイルに設定できる値か</summary>
    /// <param name="time">確かめる日時</param>
    /// <returns>Windows のファイルの日時の範囲 (1601 年以降)なら true</returns>
    private static bool IsValidTimestamp(DateTimeOffset time) => time.UtcDateTime.Year >= 1601;
}
