using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.CodeMaster.Core;

namespace MmmTool.CodeMaster.Edit;

/// <summary>
/// コードマスタの区分 1 つの名前を編集する画面の ViewModel。
/// </summary>
/// <param name="repository">コードマスタの保存先</param>
/// <remarks>
/// 保存するのは「保存」を押したときだけ。入力できない名前は、画面のエラーに出して閉じない。
/// 保存できたら、機能の後始末 (<see cref="CodeCategoryEditor.AfterSavedAsync"/>)を行い、<see cref="CloseRequested"/> で、画面を閉じてよいことを知らせる。
/// </remarks>
public sealed partial class CodeNameEditViewModel(ICodeMasterRepository repository) : ObservableObject
{
    /// <summary>編集している区分</summary>
    private CodeCategoryEditor? _editor;

    /// <summary>画面を閉じてよい</summary>
    public event EventHandler? CloseRequested;

    /// <summary>画面の題名</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>一覧の行 (表示順)</summary>
    public ObservableCollection<CodeNameItem> Items { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>「保存」を押せるか (一覧を読み込めている)</summary>
    private bool CanSave() => Items.Count > 0;

    /// <summary>区分の選択肢を読み込んで、一覧にする</summary>
    /// <param name="editor">編集する区分</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>読めなかったときは、エラーに出して、一覧を空のままにして、「保存」を押せなくする。</remarks>
    public async Task LoadAsync(CodeCategoryEditor editor)
    {
        _editor = editor;
        Title = editor.Title;
        Items.Clear();
        try
        {
            var entries = await repository.LoadOptionsAsync(editor.FeatureNo, editor.CategoryNo);
            foreach (var entry in entries)
            {
                Items.Add(new CodeNameItem(entry.CodeNo, editor.Label(entry), entry.Name));
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>名前を保存して、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        Error.Clear();
        var names = new Dictionary<int, string>();
        foreach (var item in Items)
        {
            var name = item.Name.Trim();
            if (name.Length > CodeMasterEntry.NameMaxLength)
            {
                Error.Show($"名前は {CodeMasterEntry.NameMaxLength} 文字以内で入力してください。");
                return;
            }
            if (editor.ValidateName(name) is { } problem)
            {
                Error.Show(problem);
                return;
            }
            names[item.CodeNo] = name;
        }

        try
        {
            await repository.SaveNamesAsync(editor.FeatureNo, editor.CategoryNo, names);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return;
        }

        await editor.AfterSavedAsync();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>保存せずに閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
