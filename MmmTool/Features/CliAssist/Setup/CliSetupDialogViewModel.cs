using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist.Setup;

/// <summary>CLI補助の初期設定ダイアログの ViewModel</summary>
public sealed partial class CliSetupDialogViewModel : ObservableObject
{
    /// <summary>選んでいる環境の番号 (0 = Windows、1 = WSL。<see cref="CliEnvironment"/> の値と同じ)</summary>
    /// <remarks>ラジオボタンの並び (<c>RadioButtons.SelectedIndex</c>)とつなぐため、番号で持つ。</remarks>
    [ObservableProperty]
    public partial int EnvironmentIndex { get; set; }

    /// <summary>Claude Code を使うか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial bool UsesClaudeCode { get; set; }

    /// <summary>Kiro を使うか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial bool UsesKiro { get; set; }

    /// <summary>初期化 (今の定型コマンドを作り直す)として開いたか</summary>
    /// <remarks>true のときは、消えるもの・終了するものの警告を出す。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstRun))]
    public partial bool IsReset { get; set; }

    /// <summary>初回として開いたか</summary>
    /// <remarks>true のときだけ、あとで変える方法を案内する (初期化のときは、今まさにその方法で開いているため)。</remarks>
    public bool IsFirstRun => !IsReset;

    /// <summary>決定できるか (ツールを 1 つ以上選んでいる)</summary>
    public bool CanConfirm => UsesClaudeCode || UsesKiro;

    /// <summary>開く前の状態にする</summary>
    /// <param name="environment">最初に選んでおく環境</param>
    /// <param name="isReset">初期化として開くなら true</param>
    /// <remarks>ツールは Claude Code だけを選んだ状態から始める。今の定型コマンドから、使っていたツールは読み取らない (ツールごとのフォルダは手で直せるため、確かな手がかりにならない)。</remarks>
    public void Initialize(CliEnvironment environment, bool isReset)
    {
        EnvironmentIndex = (int)environment;
        UsesClaudeCode = true;
        UsesKiro = false;
        IsReset = isReset;
    }

    /// <summary>選んだ内容を初期設定にする</summary>
    /// <returns>選んだ環境と、使うツール (<see cref="CliTool"/> の順)</returns>
    public CliSetup ToSetup()
    {
        List<CliTool> tools = [];
        if (UsesClaudeCode)
        {
            tools.Add(CliTool.ClaudeCode);
        }
        if (UsesKiro)
        {
            tools.Add(CliTool.Kiro);
        }
        return new CliSetup(EnvironmentIndex == (int)CliEnvironment.Wsl ? CliEnvironment.Wsl : CliEnvironment.Windows, tools);
    }
}
