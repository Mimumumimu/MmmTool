using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Paths;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Notes.Core;

namespace MmmTool.Notes.Main;

/// <summary>
/// ノートのページの ViewModel。表示する日・表示の切り替え・カードの操作を扱う。
/// </summary>
/// <param name="notes">ノートの読み書き</param>
/// <param name="pageBuilder">カード一覧のページを作る処理</param>
/// <param name="dialogs">ノートの入力ウィンドウを開く口</param>
/// <param name="confirm">確認ダイアログを開く口</param>
/// <param name="opener">リンクのパスを既定のアプリで開く処理</param>
/// <remarks>
/// ほかの人の更新は、ページが見えている間だけ、表示している日の分を <see cref="RefreshInterval"/> ごとに読み直して反映する (<see cref="ResumeAsync"/> / <see cref="Suspend"/>)。
/// 読み直しでページを作り直すときは、読んでいる位置と、開いているカードを保つ。日付・切り替えを変えたときは、先頭から表示し、「全文」は全部開き、「タイトルだけ」は全部閉じる。
/// </remarks>
public sealed partial class NotesViewModel(
    NoteService notes, NotePageBuilder pageBuilder, INoteDialogService dialogs, IDialogService confirm, IPathOpener opener) : ObservableObject
{
    /// <summary>ほかの人の更新を読み直す間隔</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    /// <summary>読み直しの間隔を待つタイマー (手動で読み直したとき、数え直す)</summary>
    private readonly RestartableTimer _refreshTimer = new(RefreshInterval);

    /// <summary>今、表示しているカード</summary>
    private IReadOnlyList<NoteCard> _cards = [];

    /// <summary>最後に表示したページの HTML (読み直して、同じなら、作り直さない)</summary>
    private string _lastHtml = "";

    /// <summary>読み込みの順番 (古い読み込みの結果を捨てるため)</summary>
    private int _version;

    /// <summary>読み直しを止めるためのトークンの元 (見えていないときは null)</summary>
    private CancellationTokenSource? _refreshing;

    /// <summary>カード一覧を表示する面 (ページが、作ったあとに渡す)</summary>
    public INoteCardsSurface? Surface { get; set; }

    /// <summary>ダークモードか (ページが、画面のテーマに合わせて渡す)</summary>
    public bool IsDark { get; set; }

    /// <summary>表示する日</summary>
    [ObservableProperty]
    public partial DateOnly Day { get; set; } = notes.Today;

    /// <summary>「タイトルだけ」にするか (false は「全文」)</summary>
    [ObservableProperty]
    public partial bool IsTitleOnly { get; set; }

    /// <summary>削除したものも表示するか</summary>
    [ObservableProperty]
    public partial bool ShowDeleted { get; set; }

    /// <summary>読み込み・操作の失敗</summary>
    public ErrorState Error { get; } = new();

    /// <summary>日付を変えたら、先頭から表示し直す</summary>
    /// <param name="value">新しい日</param>
    partial void OnDayChanged(DateOnly value) => ReloadAsync(preserveState: false).Forget();

    /// <summary>表示の切り替えを変えたら、先頭から表示し直す</summary>
    /// <param name="value">新しい値</param>
    partial void OnIsTitleOnlyChanged(bool value) => ReloadAsync(preserveState: false).Forget();

    /// <summary>削除したものの表示を変えたら、先頭から表示し直す</summary>
    /// <param name="value">新しい値</param>
    partial void OnShowDeletedChanged(bool value) => ReloadAsync(preserveState: false).Forget();

    /// <summary>前の日を表示する</summary>
    [RelayCommand]
    private void PreviousDay() => Day = Day.AddDays(-1);

    /// <summary>次の日を表示する</summary>
    [RelayCommand]
    private void NextDay() => Day = Day.AddDays(1);

    /// <summary>今日を表示する</summary>
    [RelayCommand]
    private void Today() => Day = notes.Today;

    /// <summary>更新ボタンを出すか (他の人の更新がある DB モードだけ。ローカルモードは false)</summary>
    public bool CanRefresh => notes.IsShared;

    /// <summary>ほかの人の更新を、今すぐ読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>定期的な読み直しと同じく、読んでいる位置と開いているカードを保つ。直後に自動の読み直しが重ならないよう、間隔は数え直す。</remarks>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error.Clear();
        _refreshTimer.Restart();
        await ReloadAsync(preserveState: true);
    }

    /// <summary>ノートを新しく書く</summary>
    /// <returns>入力ウィンドウを閉じるまでの完了を表すタスク</returns>
    [RelayCommand]
    private async Task NewNoteAsync()
    {
        if (await dialogs.ShowEditAsync(null, Day))
        {
            await ReloadAsync(preserveState: true);
        }
    }

    /// <summary>ページが見えるようになった (表示し、読み直しを始める)</summary>
    /// <returns>最初の表示の完了を表すタスク</returns>
    public async Task ResumeAsync()
    {
        Suspend();
        var source = new CancellationTokenSource();
        _refreshing = source;

        await ReloadAsync(preserveState: false);

        try
        {
            while (await _refreshTimer.WaitForNextTickAsync(source.Token))
            {
                await ReloadAsync(preserveState: true);
            }
        }
        catch (OperationCanceledException)
        {
            // ページが見えなくなった (読み直しを止める)
        }
    }

    /// <summary>ページが見えなくなった (読み直しを止める。見えていない間は、DB に問い合わせない)</summary>
    public void Suspend()
    {
        _refreshing?.Cancel();
        _refreshing?.Dispose();
        _refreshing = null;
    }

    /// <summary>テーマが変わった (ページを、新しい配色で作り直す)</summary>
    /// <returns>作り直しの完了を表すタスク</returns>
    public Task ThemeChangedAsync() => ReloadAsync(preserveState: true);

    /// <summary>カードの操作 (編集・削除・復元)を行う</summary>
    /// <param name="action">操作</param>
    /// <param name="noteId">ノートの番号</param>
    /// <returns>操作の完了を表すタスク</returns>
    public async Task HandleActionAsync(NoteAction action, int noteId)
    {
        var card = _cards.FirstOrDefault(c => c.Note.Id == noteId);
        if (card is null)
        {
            // 表示のあとに、ほかの人が変えて、もう無い。読み直して合わせる
            await ReloadAsync(preserveState: true);
            return;
        }

        try
        {
            switch (action)
            {
                case NoteAction.Edit:
                    if (await dialogs.ShowEditAsync(card.Note, Day))
                    {
                        await ReloadAsync(preserveState: true);
                    }
                    break;

                case NoteAction.Delete:
                    if (await confirm.ConfirmAsync("ノートの削除", $"「{card.Note.Title}」を削除しますか?", "削除", "キャンセル"))
                    {
                        await SetDeletedAsync(card.Note, isDeleted: true);
                    }
                    break;

                case NoteAction.Restore:
                    await SetDeletedAsync(card.Note, isDeleted: false);
                    break;
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"操作できませんでした: {ex.Message}");
        }
    }

    /// <summary>本文のリンクを、既定のアプリで開く</summary>
    /// <param name="uri">リンク先 (http・https・file)</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>ファイル (<c>file:</c>)は、パスにして開く (フォルダは、エクスプローラーで開く)。開けなかったときは、失敗を表示する。</remarks>
    public async Task OpenLinkAsync(Uri uri)
    {
        try
        {
            await opener.OpenAsync(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri);
        }
        catch (PathOpenException ex)
        {
            Error.Show($"リンクを開けませんでした: {ex.Message}");
        }
    }

    /// <summary>ノートを削除する・復元して、読み直す</summary>
    /// <param name="note">対象のノート</param>
    /// <param name="isDeleted">削除するなら true、復元するなら false</param>
    /// <returns>操作と読み直しの完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存できなかった。</exception>
    private async Task SetDeletedAsync(Note note, bool isDeleted)
    {
        if (!await notes.SetDeletedAsync(note.Id, isDeleted))
        {
            Error.Show("ノートを変更できませんでした (ほかの人がすでに変更したか、見えなくなった可能性があります)。");
        }
        await ReloadAsync(preserveState: true);
    }

    /// <summary>表示する日のノートを読み込んで、ページを作り直す</summary>
    /// <param name="preserveState">読んでいる位置と、開いているカードを保つか (false は、先頭から表示する)</param>
    /// <returns>読み込みと表示の完了を表すタスク</returns>
    /// <remarks>失敗は、画面に出す (続けられる失敗。今の表示は変えない)。読み直しの結果が前と同じなら、ページを作り直さない。</remarks>
    private async Task ReloadAsync(bool preserveState)
    {
        var version = ++_version;
        var state = preserveState && Surface is { } surface ? await surface.CaptureStateAsync() : null;

        IReadOnlyList<NoteCard> cards;
        try
        {
            cards = await notes.LoadAsync(Day, ShowDeleted);
        }
        catch (DataFileException ex)
        {
            Error.Show($"ノートを読み込めませんでした: {ex.Message}");
            return;
        }

        // 待っている間に、もっと新しい読み込みが始まっていたら、この結果は捨てる
        if (version != _version)
        {
            return;
        }

        Error.Clear();
        _cards = cards;
        var html = pageBuilder.Build(cards, new NotePageOptions(Day, IsDark, notes.IsShared, IsTitleOnly, state?.OpenIds, state?.KnownIds));
        if (preserveState && html == _lastHtml)
        {
            return;
        }

        _lastHtml = html;
        Surface?.Show(html, state?.ScrollTop ?? 0, NoteLinks.CollectHrefs(html));
    }
}
