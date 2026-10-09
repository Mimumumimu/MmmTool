using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログの ViewModel</summary>
/// <param name="quickMessages">よく使う文</param>
public sealed partial class QuickMessageDialogViewModel(CliQuickMessageService quickMessages) : ObservableObject
{
    /// <summary>編集中の一覧 (保存するまで、サービスには反映しない)</summary>
    public ObservableCollection<QuickMessageEditItem> Items { get; } = [];

    /// <summary>一覧で選ばれている項目</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial QuickMessageEditItem? SelectedItem { get; set; }

    /// <summary>項目が選ばれているか (右の入力欄を出す条件)</summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>一覧が空か</summary>
    public bool HasNoItems => Items.Count == 0;

    /// <summary>エラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>保存済みの内容で、編集中の一覧を作り直す</summary>
    public void Initialize()
    {
        Items.Clear();
        foreach (var message in quickMessages.Messages)
        {
            Items.Add(new QuickMessageEditItem { Label = message.Label ?? string.Empty, Text = message.Text ?? string.Empty });
        }
        OnPropertyChanged(nameof(HasNoItems));
        SelectedItem = Items.FirstOrDefault();
        Error.Clear();
    }

    /// <summary>項目を末尾に足して選ぶ</summary>
    [RelayCommand]
    private void Add()
    {
        var item = new QuickMessageEditItem();
        Items.Add(item);
        OnPropertyChanged(nameof(HasNoItems));
        SelectedItem = item;
    }

    /// <summary>選ばれている項目を取り除く (隣の項目を選ぶ)</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }
        var index = Items.IndexOf(item);
        Items.Remove(item);
        OnPropertyChanged(nameof(HasNoItems));
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    /// <summary>選ばれている項目を 1 つ上へ</summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    /// <summary>選ばれている項目を 1 つ下へ</summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    /// <summary>上へ動かせるか</summary>
    private bool CanMoveUp() => SelectedItem is { } item && Items.IndexOf(item) > 0;

    /// <summary>下へ動かせるか</summary>
    private bool CanMoveDown() => SelectedItem is { } item && Items.IndexOf(item) is var index && index >= 0 && index < Items.Count - 1;

    /// <summary>選ばれている項目を動かす</summary>
    /// <param name="offset">動かす数 (上は -1、下は 1)</param>
    private void Move(int offset)
    {
        if (SelectedItem is not { } item)
        {
            return;
        }
        var index = Items.IndexOf(item);
        Items.Move(index, index + offset);
        // 一覧が、動かした項目の選びを外すことがあるので、選び直す
        SelectedItem = item;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>入力を調べて保存する</summary>
    /// <returns>保存できたら true (ダイアログを閉じてよい)。文が空の項目がある・保存に失敗したときは false</returns>
    public async Task<bool> SaveAsync()
    {
        if (Items.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Text)) is { } empty)
        {
            SelectedItem = empty;
            Error.Show("文が空の項目があります。入力するか、削除してください");
            return false;
        }

        try
        {
            await quickMessages.SaveAsync(Items.Select(item => new CliQuickMessage { Label = item.Label, Text = item.Text }));
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return false;
        }
        return true;
    }
}
