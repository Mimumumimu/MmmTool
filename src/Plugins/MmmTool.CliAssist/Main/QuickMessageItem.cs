namespace MmmTool.CliAssist.Main;

/// <summary>入力欄の中に並べる、よく使う文のチップ 1 つ</summary>
/// <param name="Label">チップに表示する名前</param>
/// <param name="Text">入力欄に入れる文</param>
public sealed record QuickMessageItem(string Label, string Text);
