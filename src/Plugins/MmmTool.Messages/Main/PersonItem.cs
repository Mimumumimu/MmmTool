using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>
/// 宛先に探した人 1 人。画面に出す文字と、複数の人を選ぶ画面での選び。
/// </summary>
/// <param name="source">探した人</param>
public sealed partial class PersonItem(MessagePerson source) : ObservableObject
{
    /// <summary>元の人</summary>
    public MessagePerson Source { get; } = source;

    /// <summary>表示名</summary>
    public string DisplayName { get; } = source.DisplayName;

    /// <summary>選んでいるか。複数の人を選ぶ画面 (グループを作る・人を追加する)のチェック</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
