using System.Text.Json;
using Microsoft.Data.Sqlite;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Messages.Core;

namespace MmmTool.Messages.Cache;

/// <summary>
/// メッセージの、端末の控え。<c>Cache/MessageCache.db</c> の SQLite に、JSON の文書として置く。
/// </summary>
/// <remarks>
/// サーバーが唯一の正で、控えは、いつでも作り直せる。消しても作り直される置き場 (<c>Cache</c>)に置く。
/// 控えが読めない・書けないときは、控えなしで動く。エラーにしない。
/// 人の一覧と、会話ごとのメッセージ (新しい順の <see cref="MessageSync.CacheCountPerConversation"/> 件まで)を、ログインした人ごとに分けて持つ。
/// 全部をメモリに読み込まない。会話を開いたときに、その会話の分だけ読む。
/// </remarks>
public sealed class MessageCacheStore : IDisposable
{
    /// <summary>控えのファイルの名前</summary>
    private const string FileName = "MessageCache.db";

    /// <summary>人の一覧の文書の名前</summary>
    private const string ListKey = "list";

    /// <summary>会話の文書の名前の先頭</summary>
    private const string ConversationKeyPrefix = "c.";

    /// <summary>あるべき形。文書を 1 行ずつ置く表</summary>
    private const string Schema = "CREATE TABLE IF NOT EXISTS Entry (Key TEXT NOT NULL PRIMARY KEY, Json TEXT NOT NULL);";

    /// <summary>控えの SQLite</summary>
    private readonly SqliteDatabase _database;

    /// <summary>控えを作る</summary>
    /// <param name="environment">アプリの置き場。<c>Cache</c> のフォルダーを使う</param>
    public MessageCacheStore(AppEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _database = new SqliteDatabase(new SqliteDatabaseOptions
        {
            FilePath = Path.Combine(environment.CacheDirectory, FileName),
            SchemaSql = Schema,
        });
    }

    /// <summary>人の一覧の控えを読む</summary>
    /// <param name="userId">いまの人の番号。ほかの人の控えは、読まない</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>控え。無い・別の人の控え・古い形の控え・読めないときは null</returns>
    public async Task<CachedConversationList?> ReadListAsync(int userId, CancellationToken cancellationToken = default)
    {
        var json = await ReadAsync(ListKey, cancellationToken).ConfigureAwait(false);
        var cached = Deserialize(json, MessageCacheJsonContext.Default.CachedConversationList);
        return cached is not null && cached.UserId == userId && cached.Version == CachedConversationList.CurrentVersion ? cached : null;
    }

    /// <summary>人の一覧の控えを書く</summary>
    /// <param name="cached">書く内容</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書いたことを表すタスク。書けなくても、エラーにしない</returns>
    public Task WriteListAsync(CachedConversationList cached, CancellationToken cancellationToken = default)
        => WriteAsync(ListKey, JsonSerializer.Serialize(cached, MessageCacheJsonContext.Default.CachedConversationList), cancellationToken);

    /// <summary>会話の控えを読む</summary>
    /// <param name="userId">いまの人の番号</param>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>メッセージ。古いものが先。無い・別の人の控え・読めないときは空</returns>
    public async Task<IReadOnlyList<ChatMessage>> ReadMessagesAsync(int userId, int conversationId, CancellationToken cancellationToken = default)
    {
        var json = await ReadAsync(ConversationKeyPrefix + conversationId, cancellationToken).ConfigureAwait(false);
        var cached = Deserialize(json, MessageCacheJsonContext.Default.CachedMessages);
        return cached is not null && cached.UserId == userId ? cached.Messages : [];
    }

    /// <summary>会話の控えを書く。新しいほうから、上限の件数までを残す</summary>
    /// <param name="userId">いまの人の番号</param>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messages">いま見えているメッセージ。古いものが先</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書いたことを表すタスク。書けなくても、エラーにしない</returns>
    public Task WriteMessagesAsync(int userId, int conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var recent = messages.Count > MessageSync.CacheCountPerConversation ? messages.Skip(messages.Count - MessageSync.CacheCountPerConversation).ToList() : messages;
        return WriteAsync(
            ConversationKeyPrefix + conversationId,
            JsonSerializer.Serialize(new CachedMessages(userId, recent), MessageCacheJsonContext.Default.CachedMessages),
            cancellationToken);
    }

    /// <summary>控えを全部捨てる。ログインした人が替わったときに呼ぶ</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>捨てたことを表すタスク。捨てられなくても、エラーにしない</returns>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunAsync(async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Entry";
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            // 捨てられなくても、控えの持ち主の番号を見るので、別の人の控えは出ない
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // SqliteDatabase は、処理のたびに接続を開いて閉じるので、持ち続けるものは無い
    }

    /// <summary>文書を 1 つ読む</summary>
    /// <param name="key">文書の名前</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>JSON の文字。無い・読めないときは null</returns>
    private async Task<string?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _database.RunAsync(async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT Json FROM Entry WHERE Key = $Key";
                command.Parameters.AddWithValue("$Key", key);
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            return null;
        }
    }

    /// <summary>文書を 1 つ書く。あれば置き換える</summary>
    /// <param name="key">文書の名前</param>
    /// <param name="json">JSON の文字</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書いたことを表すタスク。書けなくても、エラーにしない</returns>
    private async Task WriteAsync(string key, string json, CancellationToken cancellationToken)
    {
        try
        {
            await _database.RunAsync(async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Entry (Key, Json) VALUES ($Key, $Json) ON CONFLICT (Key) DO UPDATE SET Json = excluded.Json";
                command.Parameters.AddWithValue("$Key", key);
                command.Parameters.AddWithValue("$Json", json);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            // 書けなくても、使うのに困らない
        }
    }

    /// <summary>JSON の文字を、型にする</summary>
    /// <typeparam name="T">型</typeparam>
    /// <param name="json">JSON の文字。null なら null を返す</param>
    /// <param name="typeInfo">型の、ソース生成の情報</param>
    /// <returns>型にした値。読めないときは null</returns>
    private static T? Deserialize<T>(string? json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>控えの読み書きの失敗か。ファイルの失敗と、SQLite の失敗を、控えなしで続けられる失敗として扱う</summary>
    /// <param name="ex">例外</param>
    /// <returns>控えなしで続けてよいとき true</returns>
    private static bool IsCacheFailure(Exception ex) => ex is DataFileException or SqliteException or IOException or UnauthorizedAccessException;
}
