namespace MmmTool.Core.CliAssist;

/// <summary>定型コマンドのタブの種類</summary>
public enum CommandCategory
{
    /// <summary>シェルで打つコマンド (AI エージェント起動前)。</summary>
    Shell,

    /// <summary>AI エージェントのセッション内で打つコマンド (起動後)。</summary>
    Session,
}
