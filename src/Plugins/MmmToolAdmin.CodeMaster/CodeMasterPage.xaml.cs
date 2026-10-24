using Microsoft.UI.Xaml.Controls;
using MmmTool.CodeMaster.Settings;

namespace MmmToolAdmin.CodeMaster;

/// <summary>選択肢のページで、編集できる区分を並べる</summary>
/// <remarks>並べる部品は、MmmTool の設定ページの「選択肢」と同じもの。ページの見出しが「選択肢」なので、部品の見出しは出さない。</remarks>
public sealed partial class CodeMasterPage : Page
{
    /// <summary>ページを作る</summary>
    /// <param name="control">区分を並べる部品</param>
    public CodeMasterPage(CodeMasterSettingsControl control)
    {
        InitializeComponent();
        control.ShowHeader = false;
        ContentHost.Content = control;
    }
}
