using System.ComponentModel;
using System.Diagnostics;

namespace MmmTool.Core.Services;

/// <summary>
/// リンクのパス（URL・ファイル・フォルダ・実行ファイル）を既定のアプリで開く。
/// </summary>
public sealed class LinkOpener
{
    /// <summary>開く。</summary>
    /// <remarks>シェル実行は呼び出し元をしばらく止めることがあるため、バックグラウンドで実行する。</remarks>
    /// <exception cref="LinkOpenException">開けなかった。</exception>
    public Task OpenAsync(string path) => Task.Run(() =>
    {
        var target = LinkTarget.Expand(path);
        if (target.Length == 0)
        {
            throw new LinkOpenException("パスが空です。");
        }

        var startInfo = new ProcessStartInfo(target) { UseShellExecute = true };

        // 実行ファイルは、自分のフォルダを作業フォルダにして起動する（隣のファイルを相対パスで読むものがあるため）
        if (File.Exists(target) && Path.GetDirectoryName(target) is { Length: > 0 } directory)
        {
            startInfo.WorkingDirectory = directory;
        }

        try
        {
            using var _ = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            throw new LinkOpenException($"「{path}」を開けませんでした。{ex.Message}", ex);
        }
    });
}

/// <summary>リンクを開けなかったことを表す例外</summary>
public sealed class LinkOpenException(string message, Exception? innerException = null) : Exception(message, innerException);
