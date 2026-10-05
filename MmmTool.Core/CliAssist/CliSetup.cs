namespace MmmTool.Core.CliAssist;

/// <summary>CLI補助の初期設定 (既定の定型コマンドを作るときに選ぶもの)</summary>
/// <param name="Environment">コマンドを動かす環境</param>
/// <param name="Tools">使うツール (1 つ以上。並びは <see cref="CliTool"/> の順)</param>
/// <remarks>定型コマンドを作ったあとは、環境だけを定型コマンドに残す (<see cref="CliCommandSet.Environment"/>)。ツールは、作ったフォルダの中身そのものなので、別には持たない。</remarks>
public sealed record CliSetup(CliEnvironment Environment, IReadOnlyList<CliTool> Tools);
