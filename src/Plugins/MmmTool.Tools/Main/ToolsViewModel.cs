using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Tools.Core;

namespace MmmTool.Tools.Main;

/// <summary>ツールページ</summary>
/// <remarks>貼った URL の文字コードをその場で日本語などに戻して見せる</remarks>
public sealed partial class ToolsViewModel : ObservableObject
{
    /// <summary>入力欄の文字列</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecodedText))]
    public partial string InputText { get; set; } = "";

    /// <summary>入力欄の文字列を戻した結果</summary>
    public string DecodedText => UrlDecoder.Decode(InputText);
}
