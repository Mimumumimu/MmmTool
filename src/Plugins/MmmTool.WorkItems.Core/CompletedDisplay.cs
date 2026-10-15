namespace MmmTool.WorkItems.Core;

/// <summary>完了して日がたった作業の見え方 (上のバーの「完了」の選択。値は、選択肢の並びと同じ)</summary>
public enum CompletedDisplay
{
    /// <summary>表示: 隠さない</summary>
    Show = 0,

    /// <summary>グループのみ表示: 作業の行を隠す (グループの行は残す)</summary>
    GroupsOnly = 1,

    /// <summary>非表示: 作業の行と、すべて完了したグループの行を隠す</summary>
    Hidden = 2,
}
