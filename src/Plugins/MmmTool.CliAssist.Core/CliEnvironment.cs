namespace MmmTool.CliAssist.Core;

/// <summary>CLI を動かす環境 (ターミナルで起動するシェル)</summary>
/// <remarks>定型コマンド (<see cref="CliCommandSet.Environment"/>)とセットで決まる。コマンドはその環境向けに書かれているため。</remarks>
public enum CliEnvironment
{
    /// <summary>Windows (PowerShell)</summary>
    Windows,

    /// <summary>WSL (既定のディストリビューションの既定のシェル)。パスは Linux の形 (<c>/mnt/d/...</c>)にして渡す</summary>
    Wsl,
}
