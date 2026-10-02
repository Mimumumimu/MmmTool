using System.Security.Cryptography;
using System.Text;

namespace MmmTool.Services;

/// <summary>
/// 同一 EXE の二重起動を防ぐ。Mutex 名を EXE パスのハッシュから作るため、
/// Debug / Release など別パスの EXE は同時に起動できる。
/// </summary>
public sealed class SingleInstanceGuard
{
    /// <summary>多重起動防止用の Mutex。</summary>
    /// <remarks>プロセス終了まで保持する（GC で解放されないようフィールドで持つ）。</remarks>
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstanceGuard()
    {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var exeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exePath.ToUpperInvariant())));
        _mutex = new Mutex(initiallyOwned: true, $@"Local\MmmTool_{exeHash}", out var createdNew);
        IsFirstInstance = createdNew;
    }
}
