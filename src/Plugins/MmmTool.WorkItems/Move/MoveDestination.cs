namespace MmmTool.WorkItems.Move;

/// <summary>移動先の選択肢 (最上位または、グループ 1 つ)</summary>
/// <param name="Id">グループの番号 (0 は最上位)</param>
/// <param name="Name">表示する名前</param>
/// <param name="Level">字下げの段 (最上位が 0)</param>
public sealed record MoveDestination(int Id, string Name, int Level)
{
    /// <summary>字下げの余白 (一覧の項目の左に入れる)</summary>
    public Microsoft.UI.Xaml.Thickness Indent => new(Level * 20, 0, 0, 0);
}
