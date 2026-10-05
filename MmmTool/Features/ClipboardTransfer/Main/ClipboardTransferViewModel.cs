using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Clipboards;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Core.ClipboardTransfer;

namespace MmmTool.Features.ClipboardTransfer.Main;

/// <summary>
/// クリップボード転送ページ。ファイルをクリップボードに載せ (送信)、クリップボードからファイルを保存する (受信)。
/// </summary>
/// <param name="transfer">変換・保存の処理</param>
/// <param name="clipboard">クリップボード</param>
/// <param name="filePicker">ファイル選択</param>
/// <param name="folderPicker">フォルダー選択</param>
/// <param name="dialogs">ダイアログ</param>
public sealed partial class ClipboardTransferViewModel(
    ClipboardTransferService transfer,
    IClipboardService clipboard,
    IFilePickerService filePicker,
    IFolderPickerService folderPicker,
    IDialogService dialogs) : ObservableObject
{
    /// <summary>結果の一覧に、ファイル名を並べる最大の件数</summary>
    private const int MaxListedNames = 5;

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>処理中か</summary>
    /// <remarks>大きなファイルは時間がかかるので、処理中は操作を受け付けない。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(SelectFilesCommand), nameof(SaveFromClipboardCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>処理中ではないか (操作できるか)</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>変換・読み書きの処理を実行中か</summary>
    /// <remarks>進行表示 (ProgressBar)に使う。ファイル選択・確認ダイアログでユーザーの入力を待っている間は false。</remarks>
    [ObservableProperty]
    public partial bool IsProcessing { get; set; }

    /// <summary>完了のお知らせを表示中か</summary>
    [ObservableProperty]
    public partial bool IsResultOpen { get; set; }

    /// <summary>完了のお知らせの見出し</summary>
    [ObservableProperty]
    public partial string ResultTitle { get; set; } = "";

    /// <summary>完了のお知らせの内容</summary>
    [ObservableProperty]
    public partial string ResultMessage { get; set; } = "";

    /// <summary>ファイルをクリップボードに載せる</summary>
    /// <param name="paths">ファイルのパス (フォルダーは除かれる)</param>
    /// <param name="skippedCount">呼び出し側で、ディスク上のファイルではないために除いた件数</param>
    /// <returns>送信の完了を表すタスク</returns>
    public async Task SendAsync(IReadOnlyList<string> paths, int skippedCount)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            var result = await ProcessAsync(transfer.EncodeAsync(paths));
            if (result.Json is null)
            {
                Error.Show(result.Error!);
                return;
            }

            clipboard.SetText(result.Json);

            var skipped = skippedCount + paths.Count - result.FileNames.Count;
            var message = $"{ListNames(result.FileNames)}\n合計 {FileSizeFormatter.Format(result.TotalBytes)}";
            if (skipped > 0)
            {
                message += $"\nフォルダー・ディスク上にないものは送れないため、{skipped} 件を除きました。";
            }
            ShowResult($"{result.FileNames.Count} 件をクリップボードにコピーしました", message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error.Show($"ファイルを読めませんでした。{ex.Message}");
        }
        catch (COMException ex)
        {
            Error.Show($"クリップボードに書けませんでした。{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>ファイルを選んで、クリップボードに載せる</summary>
    /// <returns>ファイル選択と送信の完了を表すタスク</returns>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task SelectFilesAsync()
    {
        var paths = await filePicker.PickFilesAsync();
        if (paths.Count > 0)
        {
            await SendAsync(paths, 0);
        }
    }

    /// <summary>クリップボードの転送データを、ファイルとして保存する</summary>
    /// <returns>受信の完了を表すタスク</returns>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task SaveFromClipboardAsync()
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            var text = await ProcessAsync(clipboard.GetTextAsync());
            if (string.IsNullOrEmpty(text))
            {
                Error.Show("クリップボードにテキストがありません。");
                return;
            }

            var decoded = await ProcessAsync(transfer.DecodeAsync(text));
            if (decoded.Error is not null)
            {
                Error.Show(decoded.Error);
                return;
            }

            if (decoded.Files.Count == 1)
            {
                await SaveOneAsync(decoded.Files[0]);
            }
            else
            {
                await SaveManyAsync(decoded.Files);
            }
        }
        catch (COMException ex)
        {
            Error.Show($"クリップボードを読めませんでした。{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>1 件の転送データを、保存先を選んで保存する</summary>
    /// <param name="file">保存する転送データ</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>同じ名前のファイルがあるときの確認は、保存先を選ぶダイアログが出す。</remarks>
    private async Task SaveOneAsync(TransferFile file)
    {
        if (await filePicker.PickSaveFileAsync(file.FileName) is not { } path)
        {
            return;
        }

        try
        {
            await ProcessAsync(transfer.SaveAsync(file, path));
            ShowResult("1 件を保存しました", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error.Show($"{file.FileName} を保存できませんでした。{ex.Message}");
        }
    }

    /// <summary>複数の転送データを、フォルダーを選んで保存する</summary>
    /// <param name="files">保存する転送データ</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>同じ名前のファイルがあるときは、まとめて置き換える・飛ばす・ファイルごとに決める、から選んでもらう。</remarks>
    private async Task SaveManyAsync(IReadOnlyList<TransferFile> files)
    {
        if (await folderPicker.PickFolderAsync() is not { } folder)
        {
            return;
        }

        var conflicts = ClipboardTransferService.FindConflicts(folder, files);
        var policy = FileConflictChoice.Replace;
        if (conflicts.Count > 0)
        {
            policy = await dialogs.AskFileConflictAsync(
                "ファイルの置換またはスキップ",
                $"保存先に同じ名前のファイルが {conflicts.Count} 個あります。",
                "ファイルを置き換える",
                "置き換えずスキップする",
                "キャンセル",
                "ファイルごとに決める");
            if (policy == FileConflictChoice.Cancel)
            {
                return;
            }
        }

        var saved = new List<string>();
        var failures = new List<string>();
        var skipped = 0;
        var cancelled = false;
        foreach (var file in files)
        {
            if (conflicts.Contains(file))
            {
                var choice = policy != FileConflictChoice.DecideEach
                    ? policy
                    : await dialogs.AskFileConflictAsync(
                        "ファイルの置換またはスキップ",
                        $"{file.FileName} は既に存在します。",
                        "ファイルを置き換える",
                        "置き換えずスキップする",
                        "中止");
                if (choice == FileConflictChoice.Cancel)
                {
                    cancelled = true;
                    break;
                }
                if (choice == FileConflictChoice.Skip)
                {
                    skipped++;
                    continue;
                }
            }

            try
            {
                await ProcessAsync(transfer.SaveAsync(file, ClipboardTransferService.GetDestinationPath(folder, file)));
                saved.Add(file.FileName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{file.FileName}: {ex.Message}");
            }
        }

        if (saved.Count > 0 || skipped > 0)
        {
            var message = folder;
            if (skipped > 0)
            {
                message += $"\n置き換えずに飛ばしたもの: {skipped} 件";
            }
            if (cancelled)
            {
                message += "\n途中で中止しました。";
            }
            ShowResult(saved.Count > 0 ? $"{saved.Count} 件を保存しました" : "保存したファイルはありません", message);
        }
        if (failures.Count > 0)
        {
            Error.Show($"{failures.Count} 件を保存できませんでした。\n{string.Join("\n", failures)}");
        }
    }

    /// <summary>操作を始める準備をする</summary>
    /// <returns>始められたら true。処理中なら false</returns>
    /// <remarks>前の結果・エラーを消して、処理中にする。</remarks>
    private bool TryBegin()
    {
        if (IsBusy)
        {
            return false;
        }

        Error.Clear();
        IsResultOpen = false;
        IsBusy = true;
        return true;
    }

    /// <summary>処理を実行している間、進行表示を出す</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="task">実行中の処理</param>
    /// <returns>処理の結果</returns>
    private async Task<T> ProcessAsync<T>(Task<T> task)
    {
        IsProcessing = true;
        try
        {
            return await task;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>処理を実行している間、進行表示を出す</summary>
    /// <param name="task">実行中の処理</param>
    /// <returns>処理の完了を表すタスク</returns>
    private async Task ProcessAsync(Task task)
    {
        IsProcessing = true;
        try
        {
            await task;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>完了のお知らせを表示する</summary>
    /// <param name="title">見出し</param>
    /// <param name="message">内容</param>
    private void ShowResult(string title, string message)
    {
        ResultTitle = title;
        ResultMessage = message;
        IsResultOpen = true;
    }

    /// <summary>ファイル名を、何件かまで改行で並べる</summary>
    /// <param name="names">ファイル名</param>
    /// <returns>並べた文字列。多いときは「ほか N 件」を付ける</returns>
    private static string ListNames(IReadOnlyList<string> names)
    {
        var listed = string.Join("\n", names.Take(MaxListedNames));
        return names.Count > MaxListedNames ? $"{listed}\nほか {names.Count - MaxListedNames} 件" : listed;
    }
}
