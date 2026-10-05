using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Paths;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Core.Backlog;

namespace MmmTool.Features.Backlog.Main;

/// <summary>
/// Backlog ページ。共有ファイルのフォルダー (連携用パス)の一覧を取得し、ファイルをダウンロードする。
/// </summary>
/// <param name="client">Backlog の API の呼び出し</param>
/// <param name="backlogSettings">Backlog 連携の設定</param>
/// <param name="pathOpener">URL を既定のブラウザーで開く処理</param>
/// <param name="filePicker">保存先の選択</param>
public sealed partial class BacklogViewModel(
    BacklogClient client,
    BacklogSettingsService backlogSettings,
    IPathOpener pathOpener,
    IFilePickerService filePicker) : ObservableObject
{
    /// <summary>最後に一覧を取得できたフォルダーの場所 (ダウンロードの接続先)</summary>
    private BacklogLocation? _location;

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>連携用パス (URL)</summary>
    /// <remarks>前回、一覧を取得できたときの値が入る。</remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand), nameof(OpenInBrowserCommand))]
    public partial string Url { get; set; } = backlogSettings.Url;

    /// <summary>処理中か</summary>
    /// <remarks>通信やダウンロードの間は、操作を受け付けない。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand), nameof(OpenInBrowserCommand), nameof(DownloadCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>処理中ではないか (操作できるか)</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>通信・書き込みを実行中か</summary>
    /// <remarks>進行表示 (ProgressBar)に使う。保存先を選んでもらっている間は false。</remarks>
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

    /// <summary>一覧を取得できたことがあるか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaceholderVisible), nameof(IsEmptyVisible))]
    public partial bool HasFetched { get; set; }

    /// <summary>取得したファイルの一覧</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyVisible))]
    public partial IReadOnlyList<BacklogFileItem> Files { get; set; } = [];

    /// <summary>一覧の見出しに出す、取得したフォルダー (プロジェクト / フォルダー)</summary>
    [ObservableProperty]
    public partial string ConnectionText { get; set; } = "";

    /// <summary>一覧の見出しに出す、件数</summary>
    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    /// <summary>まだ一覧を取得していないときの案内を出すか</summary>
    public bool IsPlaceholderVisible => !HasFetched;

    /// <summary>取得したが、ファイルが 1 件も無いときの案内を出すか</summary>
    public bool IsEmptyVisible => HasFetched && Files.Count == 0;

    /// <summary>連携用パスが入力されていて、操作できるか</summary>
    private bool CanUseUrl => IsIdle && !string.IsNullOrWhiteSpace(Url);

    /// <summary>連携用パスのフォルダーの、共有ファイルの一覧を取得する</summary>
    /// <returns>取得の完了を表すタスク</returns>
    /// <remarks>取得できたら、連携用パスを次回のために保存する (履歴は持たず、最後の 1 件を上書きする)。</remarks>
    [RelayCommand(CanExecute = nameof(CanUseUrl))]
    private async Task FetchAsync()
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            var parsed = BacklogLocationParser.Parse(Url);
            if (parsed.Location is not { } location)
            {
                Error.Show(parsed.Error!);
                return;
            }
            if (!TryGetApiKey(out var apiKey))
            {
                return;
            }

            IReadOnlyList<BacklogSharedFile> files;
            IsProcessing = true;
            try
            {
                files = await client.ListFilesAsync(location, apiKey);
            }
            finally
            {
                IsProcessing = false;
            }

            _location = location;
            Files = files.Select(file => new BacklogFileItem(file, DownloadCommand)).ToList();
            ConnectionText = location.DisplayText;
            SummaryText = $"{files.Count} 件のファイル";
            HasFetched = true;

            await backlogSettings.SetUrlAsync(Url.Trim());
        }
        catch (BacklogException ex)
        {
            Error.Show(ex.Message);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>連携用パスの画面を、既定のブラウザーで開く</summary>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>追加・削除は公開 API で行えないので、Backlog の画面で行ってもらう。</remarks>
    [RelayCommand(CanExecute = nameof(CanUseUrl))]
    private async Task OpenInBrowserAsync()
    {
        Error.Clear();
        IsResultOpen = false;

        var url = Url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            Error.Show("URL が正しくありません。https:// から始まる URL を入力してください。");
            return;
        }

        try
        {
            await pathOpener.OpenAsync(url);
        }
        catch (PathOpenException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>ファイルを、保存先を選んでダウンロードする</summary>
    /// <param name="item">ダウンロードするファイルの行</param>
    /// <returns>ダウンロードの完了を表すタスク</returns>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task DownloadAsync(BacklogFileItem item)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            // 一覧があるのは、取得に成功して場所を覚えたあと
            if (_location is not { } location || !TryGetApiKey(out var apiKey))
            {
                return;
            }
            if (await filePicker.PickSaveFileAsync(item.Name) is not { } path)
            {
                return;
            }

            IsProcessing = true;
            try
            {
                await client.DownloadAsync(location, item.Source, apiKey, path);
            }
            finally
            {
                IsProcessing = false;
            }
            ShowResult("ダウンロードしました", $"{path}\n{item.SizeText}");
        }
        catch (BacklogException ex)
        {
            Error.Show(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error.Show($"{item.Name} を保存できませんでした。{ex.Message}");
        }
        finally
        {
            IsBusy = false;
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

    /// <summary>登録済みの API キーを取得する</summary>
    /// <param name="apiKey">取得できた API キー</param>
    /// <returns>取得できたら true。登録が無い・読めないときは、エラーを画面に出して false</returns>
    /// <remarks>キーは押したときに読む (ページは使い回されるので、設定ページで登録・変更した直後でも、新しい値になる)。</remarks>
    private bool TryGetApiKey([NotNullWhen(true)] out string? apiKey)
    {
        try
        {
            apiKey = backlogSettings.GetApiKey();
        }
        catch (SecretStoreException ex)
        {
            apiKey = null;
            Error.Show(ex.Message);
            return false;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Error.Show("API キーが登録されていません。設定ページで登録してください。");
            return false;
        }
        return true;
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
}
