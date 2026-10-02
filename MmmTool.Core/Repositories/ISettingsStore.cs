using System.Text.Json.Serialization.Metadata;

namespace MmmTool.Core.Repositories;

/// <summary>
/// キーに対して任意の型の値を保存・取得する、アプリ共通の汎用設定ストア。
/// </summary>
/// <remarks>
/// 機能ごとの設定サービスは、キーに接頭辞を付けて委譲すると衝突しない（例: <c>WindowPosition.&lt;キー&gt;</c>、<c>Reminder.&lt;項目&gt;</c>）。
/// 値は JSON で持つので、string / bool / int / long / double はそのまま使える。それ以外の型は、
/// 使う側が <c>JsonSerializable</c> で登録した <see cref="JsonTypeInfo{T}"/> を渡す。
/// 読み書きはスレッドセーフ。ファイルが無い・空・壊れているときは空の設定として扱い、例外は投げない。
/// </remarks>
public interface ISettingsStore
{
    /// <summary>ファイルが壊れていて読めなかったときのメッセージ。正常なら null。</summary>
    string? LoadError { get; }

    /// <summary>値を取得する。無い・型が合わないときは既定値を返す（基本型用）。</summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>値を取得する。無い・型が合わないときは既定値を返す。</summary>
    T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo);

    /// <summary>値を保存する（基本型用）。保存し終えるまで待つ。</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>値を保存する。保存し終えるまで待つ。</summary>
    Task SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default);

    /// <summary>キーが存在するか</summary>
    bool Contains(string key);

    /// <summary>キーを削除する。存在して削除したときは true。</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}
