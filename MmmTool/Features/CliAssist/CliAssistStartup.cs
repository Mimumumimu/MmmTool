using MmmTool.Core.CliAssist;
using MmmTool.Shell;

namespace MmmTool.Features.CliAssist;

/// <summary>CLI補助の起動時の準備（利用状態の読み込み）</summary>
/// <param name="settings">CLI補助の利用状態</param>
/// <remarks>画面を作る前に読み込む（前回の作業ディレクトリでシェルを始めるため）。失敗は画面側で通知する。</remarks>
public sealed class CliAssistStartup(CliSettingsService settings) : IStartupTask
{
    /// <inheritdoc />
    public Task StartAsync() => settings.LoadAsync();
}
