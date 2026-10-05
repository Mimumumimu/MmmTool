namespace MmmTool.Core.Backlog;

/// <summary>
/// Backlog の共有ファイルのフォルダーの場所 (スペース・プロジェクト・フォルダーパス)。
/// </summary>
/// <param name="Domain">スペースのドメイン (例: <c>xxx.backlog.jp</c>)</param>
/// <param name="ProjectKey">プロジェクトキー (デコード済み)</param>
/// <param name="FolderPath">フォルダーパス (デコード済み。区切りは <c>/</c>。先頭・末尾の <c>/</c> は含まない。ルートは空)</param>
public sealed record BacklogLocation(string Domain, string ProjectKey, string FolderPath)
{
    /// <summary>画面に出す、プロジェクトとフォルダーの表記 (<c>プロジェクト / フォルダー</c>)</summary>
    public string DisplayText => FolderPath.Length == 0 ? ProjectKey : $"{ProjectKey} / {FolderPath.Replace("/", " / ", StringComparison.Ordinal)}";
}
