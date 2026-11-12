namespace MmmTool.CliAssist.Core;

/// <summary>CLI を動かす環境</summary>
/// <remarks>
/// 定型コマンド (<see cref="CliCommandSet.Environment"/>)とセットで決まる
/// コマンドはその環境向けに書かれているため
/// </remarks>
public enum CliEnvironment
{
    /// <summary>Windows</summary>
    Windows,

    /// <summary>WSL で、既定のディストリビューションの既定のシェル</summary>
    /// <remarks>パスは Linux の形にして渡す</remarks>
    Wsl,
}
