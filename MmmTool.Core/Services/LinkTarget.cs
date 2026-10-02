namespace MmmTool.Core.Services;

/// <summary>リンクのパスが指す先の種類。</summary>
public enum LinkTargetKind
{
    /// <summary>パスが空。</summary>
    Empty,

    Url,

    Folder,

    /// <summary>実行ファイル（.exe・.bat 等）。</summary>
    Executable,

    File,

    /// <summary>ファイルにもフォルダにも見つからない。</summary>
    NotFound,
}

/// <summary>
/// リンクのパスの解釈（環境変数の展開・種類の判定）。
/// </summary>
public static class LinkTarget
{
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msc",
    };

    /// <summary>開くときの実際のパス（前後の空白・引用符を除き、環境変数を展開する）。</summary>
    public static string Expand(string path)
        => Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

    /// <summary>パスの種類を判定する。</summary>
    /// <remarks>ファイルの有無を調べるため、ネットワーク上のパスでは時間がかかることがある。UI スレッドからは呼ばない。</remarks>
    public static LinkTargetKind Classify(string path)
    {
        var expanded = Expand(path);
        if (expanded.Length == 0)
        {
            return LinkTargetKind.Empty;
        }

        // https: や mailto: 等のスキーム付き。C:\ のようなドライブ文字や \\server\ は file として扱われるので除く
        if (Uri.TryCreate(expanded, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return LinkTargetKind.Url;
        }

        if (Directory.Exists(expanded))
        {
            return LinkTargetKind.Folder;
        }

        if (File.Exists(expanded))
        {
            return ExecutableExtensions.Contains(Path.GetExtension(expanded)) ? LinkTargetKind.Executable : LinkTargetKind.File;
        }

        return LinkTargetKind.NotFound;
    }
}
