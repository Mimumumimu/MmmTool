using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送信のログを、ローカルのファイルに書く (1 日 1 ファイル。1 行 1 件の JSON Lines)。
/// </summary>
/// <param name="directory">ログを置くフォルダー (<c>Data/Logs</c>)</param>
/// <remarks>
/// ファイル名は <c>send_yyyy-MM-dd.jsonl</c> (送った日のローカルの日付。エラーのログ <c>yyyy-MM-dd.log</c> とは、名前が重ならない)。
/// ログは永遠に残す。今日から 30 日以上前の日のファイルは、<see cref="MoveOldFiles"/> が <c>old</c> フォルダーへ移す。
/// アプリは、ログを書くだけで読まない (何かあったときに、人がファイルを読む)。このコンピューターの中だけのログで、別のコンピューターで動かした MmmBatch のログは、そのコンピューターのファイルにある。
/// </remarks>
public sealed class FileSendLog(string directory) : ISendLog
{
    /// <summary>ログのファイル名の先頭</summary>
    private const string FilePrefix = "send_";

    /// <summary>ログのファイルの拡張子</summary>
    private const string FileExtension = ".jsonl";

    /// <summary>ファイルを移すフォルダー名</summary>
    private const string OldFolderName = "old";

    /// <summary>ファイルを残す日数 (今日を含む。これより古い日のファイルは <c>old</c> へ移す)</summary>
    public const int KeepDays = 30;

    /// <summary>書き込みを 1 つずつにするロック</summary>
    private readonly Lock _gate = new();

    /// <inheritdoc />
    /// <exception cref="DataFileException">ファイルに書けなかった (メッセージは画面に出せる)。</exception>
    public Task AppendAsync(SendLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var path = PathFor(DateOnly.FromDateTime(entry.At.LocalDateTime));
        var line = JsonSerializer.Serialize(entry, SendLogJsonContext.Default.SendLogEntry) + "\n";
        try
        {
            Directory.CreateDirectory(directory);
            lock (_gate)
            {
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DataFileException($"送信のログを書けませんでした ({ex.Message})。", ex);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ファイルを移せなかった (メッセージは画面に出せる)。</exception>
    public int MoveOldFiles(DateOnly today)
    {
        var moved = 0;
        try
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            // 今日を含めて KeepDays 日分を残す。それより古い日のファイルを old へ移す
            var oldest = today.AddDays(-(KeepDays - 1));
            var oldDirectory = Path.Combine(directory, OldFolderName);
            foreach (var file in Directory.GetFiles(directory, FilePrefix + "*" + FileExtension))
            {
                if (DateOf(file) is not { } date || date >= oldest)
                {
                    continue;
                }

                Directory.CreateDirectory(oldDirectory);
                var target = Path.Combine(oldDirectory, Path.GetFileName(file));
                lock (_gate)
                {
                    File.Move(file, target, overwrite: true);
                }
                moved++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DataFileException($"古い送信のログを old フォルダーへ移せませんでした ({ex.Message})。", ex);
        }
        return moved;
    }

    /// <summary>日付から、ログのファイルのパスを作る</summary>
    /// <param name="date">日付</param>
    /// <returns>ログのファイルのパス</returns>
    private string PathFor(DateOnly date)
        => Path.Combine(directory, FilePrefix + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + FileExtension);

    /// <summary>ログのファイルの名前から、日付を取り出す</summary>
    /// <param name="path">ログのファイルのパス</param>
    /// <returns>日付。名前の形が違えば null</returns>
    private static DateOnly? DateOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith(FilePrefix, StringComparison.Ordinal)
            && DateOnly.TryParseExact(name[FilePrefix.Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}

/// <summary>送信のログの JSON のソース生成 (リフレクションを使わない)</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(JsonStringEnumConverter<SendStatus>), typeof(JsonStringEnumConverter<MmmTool.NotificationChannels.Core.NotificationChannelKind>)])]
[JsonSerializable(typeof(SendLogEntry))]
internal sealed partial class SendLogJsonContext : JsonSerializerContext;
