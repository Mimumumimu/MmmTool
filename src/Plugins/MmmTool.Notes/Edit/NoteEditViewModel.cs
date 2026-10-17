using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Notes.Core;

namespace MmmTool.Notes.Edit;

/// <summary>
/// ノートの入力画面。ノート 1 件を新規登録・編集して保存する。
/// </summary>
/// <param name="notes">ノートの読み書き</param>
/// <remarks>
/// 開始日と終了日は、一方を変えて前後が逆になったら、もう一方を同じ日に合わせる。終了日は空にできる (無期限)。
/// 公開範囲を変えられるのは、書いた人だけ (ほかの人が、全員に見えるノートを、自分だけのものにして隠さないため)。
/// 保存に成功したら <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class NoteEditViewModel(NoteService notes) : ObservableObject
{
    /// <summary>重要度の選択の位置 (最低 = 0 … 最高 = 4)と、<see cref="NotePriority"/> の差</summary>
    private const int PriorityOffset = 1;

    /// <summary>編集対象。新規なら null</summary>
    private Note? _target;

    /// <summary>開始日・終了日をそろえている最中か (そろえた変更が、さらに変更を呼ばないように)</summary>
    private bool _syncingDates;

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>保存したときは true、キャンセルのときは false を渡す。</remarks>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>複数の人で共有するか (DB モード。公開範囲を出す)</summary>
    public bool IsShared => notes.IsShared;

    /// <summary>公開範囲を変えられるか (共有するときの、新規か、書いた人の編集)</summary>
    public bool CanChangeVisibility => IsShared && (_target is null || _target.AuthorUserId == notes.CurrentUserId);

    /// <summary>画面のタイトル</summary>
    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = "";

    /// <summary>タイトル</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>本文 (書いたとおりの文字。右クリックの助けが、書式のタグを入れる)</summary>
    [ObservableProperty]
    public partial string Body { get; set; } = "";

    /// <summary>開始日</summary>
    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    /// <summary>終了日。空は無期限</summary>
    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    /// <summary>重要度の選択の位置 (最低 = 0 … 最高 = 4。初期は「中」)</summary>
    [ObservableProperty]
    public partial int PriorityIndex { get; set; } = (int)NotePriority.Normal - PriorityOffset;

    /// <summary>自分だけに表示するか (false は全員。DB モードだけ)</summary>
    [ObservableProperty]
    public partial bool IsPrivate { get; set; }

    /// <summary>保存の失敗</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>新規の登録・編集の内容を読み込む</summary>
    /// <param name="note">編集するノート。新規なら null</param>
    /// <param name="defaultStartDate">新規のときの、開始日の初期値</param>
    /// <remarks>新規の終了日は、開始日と同じ (1 日だけ。無期限は、利用者が空にして選ぶ)。</remarks>
    public void Load(Note? note, DateOnly defaultStartDate)
    {
        _target = note;
        SaveError.Clear();
        _syncingDates = true;
        try
        {
            if (note is null)
            {
                WindowTitle = "ノート入力";
                Title = "";
                Body = "";
                StartDate = ToOffset(defaultStartDate);
                EndDate = null;
                PriorityIndex = (int)NotePriority.Normal - PriorityOffset;
                IsPrivate = false;
            }
            else
            {
                WindowTitle = "ノート編集";
                Title = note.Title;
                Body = note.Body;
                StartDate = ToOffset(note.StartDate);
                EndDate = note.IsOpenEnded ? null : ToOffset(note.EndDate);
                PriorityIndex = (int)note.Priority - PriorityOffset;
                IsPrivate = note.IsPrivate;
            }
        }
        finally
        {
            _syncingDates = false;
        }
        OnPropertyChanged(nameof(CanChangeVisibility));
    }

    /// <summary>開始日を変えたら、終了日より後にならないよう、終了日を合わせる</summary>
    /// <param name="value">新しい開始日</param>
    partial void OnStartDateChanged(DateTimeOffset? value)
    {
        if (!_syncingDates && value is { } start && EndDate is { } end && start.Date > end.Date)
        {
            _syncingDates = true;
            EndDate = start;
            _syncingDates = false;
        }
    }

    /// <summary>終了日を変えたら、開始日より前にならないよう、開始日を合わせる</summary>
    /// <param name="value">新しい終了日</param>
    partial void OnEndDateChanged(DateTimeOffset? value)
    {
        if (!_syncingDates && value is { } end && StartDate is { } start && end.Date < start.Date)
        {
            _syncingDates = true;
            StartDate = end;
            _syncingDates = false;
        }
    }

    /// <summary>終了日をクリアする (無期限にする)</summary>
    [RelayCommand]
    private void ClearEndDate() => EndDate = null;

    /// <summary>保存して閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>失敗は、画面に出す (閉じずに、書いた内容を残す)。</remarks>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (StartDate is not { } start)
        {
            SaveError.Show("開始日を入力してください。");
            return;
        }

        var input = new NoteInput
        {
            Title = Title,
            Body = Body,
            Priority = (NotePriority)(PriorityIndex + PriorityOffset),
            IsPrivate = IsShared && IsPrivate,
            StartDate = DateOnly.FromDateTime(start.Date),
            EndDate = EndDate is { } end ? DateOnly.FromDateTime(end.Date) : DateOnly.MaxValue,
        };
        if (input.Validate() is { } error)
        {
            SaveError.Show(error);
            return;
        }

        try
        {
            var saved = _target is null
                ? await notes.AddAsync(input) is not null
                : await notes.UpdateAsync(_target.Id, input);
            if (!saved)
            {
                SaveError.Show("ノートを保存できませんでした (ほかの人が変更したか、見えなくなった可能性があります)。");
                return;
            }
        }
        catch (Exception ex) when (ex is DataFileException or ArgumentException)
        {
            SaveError.Show(ex.Message);
            return;
        }

        CloseRequested?.Invoke(this, true);
    }

    /// <summary>キャンセルして閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    /// <summary>日付を、日付の欄に渡す形にする</summary>
    /// <param name="date">日付</param>
    /// <returns>その日の 0 時 (この PC の時差)</returns>
    private static DateTimeOffset ToOffset(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));
}
