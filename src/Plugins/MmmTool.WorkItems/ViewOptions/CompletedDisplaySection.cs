using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ViewOptions;

/// <summary>「表示」の入れ物にある、完了した作業の見え方を選ぶ部 (見出しと、3 つのラジオ)</summary>
/// <remarks>
/// 作業管理の「過去の完了」とスケジュールの「完了」が共有する。選択肢の並びは <see cref="CompletedDisplay"/> の値と同じで、保存しない。
/// 見出しの言葉と補足は、画面ごとに渡す。
/// </remarks>
public sealed class CompletedDisplaySection : StackPanel
{
    /// <summary>選択肢の名前 (<see cref="CompletedDisplay"/> の値の順)</summary>
    private static readonly string[] Names = ["表示", "グループのみ表示", "非表示"];

    /// <summary>ラジオ</summary>
    private readonly RadioButtons _radios = new();

    /// <summary>選ばれたときに呼ぶ処理 (選択肢の番号を受け取る)</summary>
    private readonly Action<int> _selected;

    /// <summary>部を作る</summary>
    /// <param name="header">見出しの言葉</param>
    /// <param name="toolTip">ラジオに付けるツールチップ (無ければ null)</param>
    /// <param name="selected">選ばれたときに呼ぶ処理 (選択肢の番号を受け取る)</param>
    public CompletedDisplaySection(string header, string? toolTip, Action<int> selected)
    {
        _selected = selected;
        Spacing = 4;
        Children.Add(new TextBlock { Text = header, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
        foreach (var name in Names)
        {
            _radios.Items.Add(name);
        }
        _radios.MaxColumns = 1;
        AutomationProperties.SetName(_radios, header);
        if (toolTip is not null)
        {
            ToolTipService.SetToolTip(_radios, toolTip);
        }
        _radios.SelectionChanged += (_, _) =>
        {
            if (_radios.SelectedIndex >= 0)
            {
                _selected(_radios.SelectedIndex);
            }
        };
        Children.Add(_radios);
    }

    /// <summary>今の選択を、ラジオに表示する (入れ物を開くたびに呼ぶ)</summary>
    /// <param name="index">選択肢の番号</param>
    public void Select(int index) => _radios.SelectedIndex = index;
}
