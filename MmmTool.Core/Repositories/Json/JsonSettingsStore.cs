using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MmmTool.Core.Repositories.Json;

/// <summary>
/// 汎用設定ストアの JSON ファイル実装。全設定を <c>Data/AppSettings.json</c> の 1 ファイルに集約する。
/// </summary>
/// <remarks>
/// 内部では「キー → JSON 要素」の辞書をメモリに持ち、取得時に目的の型へ変換する。最初のアクセスで 1 度だけ読み込む。
/// ファイルが無い・空・壊れているときは空として扱う。壊れていた場合は次の保存で上書きされる（<see cref="LoadError"/> には理由を残す）。
/// </remarks>
public sealed class JsonSettingsStore(JsonFileStore store) : ISettingsStore
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "AppSettings.json";

    /// <summary>辞書の読み書きを守るロック</summary>
    private readonly object _gate = new();

    /// <summary>保存の順序を守るロック（古い内容が後から書かれないように）</summary>
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>キー → 値の辞書。最初のアクセスまでは null</summary>
    private Dictionary<string, JsonElement>? _values;

    /// <inheritdoc />
    public string? LoadError { get; private set; }

    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue) => Get(key, defaultValue, GetBuiltInTypeInfo<T>());

    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo)
    {
        JsonElement element;
        lock (_gate)
        {
            if (!EnsureLoaded().TryGetValue(key, out element))
            {
                return defaultValue;
            }
        }

        try
        {
            var value = element.Deserialize(typeInfo);
            return value is null ? defaultValue : value;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<T>(), cancellationToken);

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var element = JsonSerializer.SerializeToElement(value, typeInfo);
        return UpdateAsync(values => values[key] = element, cancellationToken);
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        lock (_gate)
        {
            return EnsureLoaded().ContainsKey(key);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await UpdateAsync(values => removed = values.Remove(key), cancellationToken);
        return removed;
    }

    /// <summary>辞書を書き換えて保存する</summary>
    /// <remarks>書き換えたあとの内容を保存の順番どおりに書く。何も変わらなかったときも書くが、実害はない。</remarks>
    private async Task UpdateAsync(Action<Dictionary<string, JsonElement>> change, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, JsonElement> snapshot;
            lock (_gate)
            {
                var values = EnsureLoaded();
                change(values);
                snapshot = new Dictionary<string, JsonElement>(values);
            }

            await store.WriteAsync(FileName, snapshot, CoreJsonContext.Readable.DictionaryStringJsonElement, cancellationToken);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>未読み込みなら読み込んで辞書を返す。呼ぶ側は <c>_gate</c> を取っておくこと</summary>
    private Dictionary<string, JsonElement> EnsureLoaded()
    {
        if (_values is not null)
        {
            return _values;
        }

        try
        {
            _values = store.Read(FileName, CoreJsonContext.Readable.DictionaryStringJsonElement) ?? [];
        }
        catch (DataFileException ex)
        {
            LoadError = ex.Message;
            _values = [];
        }

        return _values;
    }

    /// <summary>基本型の <see cref="JsonTypeInfo{T}"/> を返す</summary>
    /// <exception cref="InvalidOperationException">基本型以外で、TypeInfo の指定が無いとき（呼び出し側のプログラムの誤り）。</exception>
    private static JsonTypeInfo<T> GetBuiltInTypeInfo<T>()
        => CoreJsonContext.Readable.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"{typeof(T)} は JsonTypeInfo を渡さずに使えません。");
}
