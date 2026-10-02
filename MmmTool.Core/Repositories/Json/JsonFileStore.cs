using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MmmTool.Core.Repositories.Json;

/// <summary>
/// データフォルダ内の JSON ファイルを読み書きする。書き込みは一時ファイルに書いてから置き換え、途中で失敗しても元のファイルを壊さない。
/// </summary>
public sealed class JsonFileStore(string dataDirectory)
{
    /// <summary>書き込みの同時実行を防ぐロック</summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>ファイルが存在するか</summary>
    public bool Exists(string fileName) => File.Exists(GetPath(fileName));

    /// <summary>同期で読み込む</summary>
    /// <remarks>ファイルが無ければ null。小さなファイルを起動時などに UI スレッドで読む用途向け。</remarks>
    public T? Read<T>(string fileName, JsonTypeInfo<T> typeInfo)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, typeInfo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new DataFileException($"{fileName} を読み込めませんでした。{ex.Message}", ex);
        }
    }

    /// <summary>読み込む</summary>
    /// <remarks>ファイルが無ければ null。</remarks>
    public async Task<T?> ReadAsync<T>(string fileName, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new DataFileException($"{fileName} を読み込めませんでした。{ex.Message}", ex);
        }
    }

    /// <summary>書き込む。一時ファイルに書いてから置き換える</summary>
    public async Task WriteAsync<T>(string fileName, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var path = GetPath(fileName);
        var tempPath = path + ".tmp";

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(dataDirectory);
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DataFileException($"{fileName} を保存できませんでした。{ex.Message}", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>ファイルのフルパスを返す</summary>
    private string GetPath(string fileName) => Path.Combine(dataDirectory, fileName);
}
