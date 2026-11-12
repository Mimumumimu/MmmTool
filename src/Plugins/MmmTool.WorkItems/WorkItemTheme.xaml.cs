using Microsoft.UI.Xaml;

namespace MmmTool.WorkItems;

/// <summary>作業管理の表とスケジュールが共有する色の辞書</summary>
/// <remarks>ページの <c>MergedDictionaries</c> に入れて使う</remarks>
public sealed partial class WorkItemTheme : ResourceDictionary
{
    /// <summary>辞書を作る</summary>
    public WorkItemTheme()
    {
        InitializeComponent();
    }
}
