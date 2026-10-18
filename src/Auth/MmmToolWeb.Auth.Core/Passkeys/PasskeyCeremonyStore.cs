using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MmmToolWeb.Auth.Core.Passkeys;

/// <summary>
/// パスキーの登録・ログインの途中の状態 (最初の問い合わせで作った選択肢)を、完了の呼び出しまで覚えておく。
/// </summary>
/// <remarks>
/// 状態は、1 回取り出すと消える。同じ呼び出しを 2 回送っても、2 回目は通らない (リプレイの防止)。
/// プロセスのメモリだけに持つので、サーバーを再起動すると消える (途中の操作は、最初からやり直す)。
/// </remarks>
public sealed class PasskeyCeremonyStore
{
    /// <summary>状態を覚えておく時間。</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>番号ごとの状態と、期限 (UTC)。</summary>
    private readonly ConcurrentDictionary<string, (object State, DateTime ExpiresAt)> _items = new();

    /// <summary>
    /// 状態を覚えて、取り出すための番号を返す。
    /// </summary>
    /// <param name="state">覚える状態。</param>
    /// <returns>取り出すための番号 (推測できないランダムな値)。</returns>
    public string Put(object state)
    {
        RemoveExpired();
        var id = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        _items[id] = (state, DateTime.UtcNow + Lifetime);
        return id;
    }

    /// <summary>
    /// 状態を取り出して、忘れる。
    /// </summary>
    /// <typeparam name="T">期待する状態の型。</typeparam>
    /// <param name="id"><see cref="Put"/> が返した番号。</param>
    /// <returns>状態。番号が違う・期限切れ・型が違うときは <c>null</c>。</returns>
    public T? Take<T>(string id) where T : class
    {
        if (!_items.TryRemove(id, out var item))
        {
            return null;
        }
        return item.ExpiresAt >= DateTime.UtcNow ? item.State as T : null;
    }

    /// <summary>
    /// 期限が切れた状態を捨てる。
    /// </summary>
    private void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _items)
        {
            if (pair.Value.ExpiresAt < now)
            {
                _items.TryRemove(pair.Key, out _);
            }
        }
    }
}
