using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Services.Terminal;

namespace MmmTool.ViewModels;

public sealed partial class CliAssistViewModel : ObservableObject
{
    public CliAssistViewModel(ITerminalSession terminal)
    {
        Terminal = terminal;
        InputText = string.Empty;
    }

    /// <summary>中央のターミナルで動くシェルのセッション。</summary>
    public ITerminalSession Terminal { get; }

    /// <summary>下部の入力欄のテキスト。</summary>
    [ObservableProperty]
    public partial string InputText { get; set; }

    // ②で実装: 入力欄のテキスト（と添付）をターミナルへ送る
    [RelayCommand]
    private void Send()
    {
    }

    // ②で実装: 送信履歴を表示する
    [RelayCommand]
    private void ShowHistory()
    {
    }
}
