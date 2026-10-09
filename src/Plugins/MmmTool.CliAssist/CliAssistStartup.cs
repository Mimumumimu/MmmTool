using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Shells;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist;

/// <summary>CLI補助の起動時の準備 (利用状態・よく使う文の読み込み、既定のシェルの探索)</summary>
/// <param name="settings">CLI補助の利用状態</param>
/// <param name="quickMessages">よく使う文</param>
/// <remarks>画面を作る前に読み込む (前回の作業ディレクトリでシェルを始めるため)。失敗は画面側で通知する。既定のシェルの探索は PATH の全項目へ触れるので、UI スレッドで初めて読まないよう、ここでバックグラウンドから済ませておく。</remarks>
public sealed class CliAssistStartup(CliSettingsService settings, CliQuickMessageService quickMessages) : IStartupTask
{
    /// <inheritdoc />
    public async Task StartAsync()
    {
        var shell = Task.Run(() => ShellLocator.Default);
        await settings.LoadAsync();
        await quickMessages.LoadAsync();
        await shell;
    }
}
