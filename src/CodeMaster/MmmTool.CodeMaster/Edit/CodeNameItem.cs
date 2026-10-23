using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.CodeMaster.Edit;

/// <summary>編集画面の一覧の 1 行で、固定の表記と、直せる名前</summary>
/// <param name="codeNo">コード番号</param>
/// <param name="label">名前の前に固定で出す表記</param>
/// <param name="name">保存済みの名前</param>
public sealed partial class CodeNameItem(int codeNo, string label, string name) : ObservableObject
{
    /// <summary>コード番号</summary>
    public int CodeNo { get; } = codeNo;

    /// <summary>名前の前に固定で出す表記</summary>
    public string Label { get; } = label;

    /// <summary>保存済みの名前</summary>
    public string OriginalName { get; } = name;

    /// <summary>入力中の名前</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = name;
}
