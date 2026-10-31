using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.FocusTriggers;

/// <summary>通知する文字列の編集ダイアログの ViewModel</summary>
/// <param name="focusTriggers">通知する文字列</param>
public sealed partial class FocusTriggerDialogViewModel(CliFocusTriggerService focusTriggers) : ObservableObject
{
    /// <summary>編集中の一覧 (保存するまで、サービスには反映しない)</summary>
    public ObservableCollection<FocusTriggerEditItem> Items { get; } = [];

    /// <summary>一覧が空か</summary>
    public bool HasNoItems => Items.Count == 0;

    /// <summary>エラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>項目の入力欄にフォーカスを移すよう、View に頼む</summary>
    /// <remarks>ViewModel は UI に触れないため。項目を足したときと、空の項目を見つけたときに頼む。</remarks>
    public event EventHandler<FocusTriggerEditItem>? FocusRequested;

    /// <summary>保存済みの内容で、編集中の一覧を作り直す</summary>
    public void Initialize()
    {
        Items.Clear();
        foreach (var trigger in focusTriggers.Triggers)
        {
            var item = CreateItem(trigger.Text!);
            item.MoveToTerminal = trigger.MoveToTerminal;
            Items.Add(item);
        }
        OnPropertyChanged(nameof(HasNoItems));
        Error.Clear();
    }

    /// <summary>項目を末尾に足して、入力欄にフォーカスを移す</summary>
    [RelayCommand]
    private void Add()
    {
        var item = CreateItem(string.Empty);
        Items.Add(item);
        OnPropertyChanged(nameof(HasNoItems));
        FocusRequested?.Invoke(this, item);
    }

    /// <summary>入力を調べて保存する</summary>
    /// <returns>保存できたら true で、ダイアログを閉じてよい。文字列が空の項目がある・保存に失敗したときは false</returns>
    public async Task<bool> SaveAsync()
    {
        if (Items.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Text)) is { } empty)
        {
            Error.Show("文字列が空の項目があります。入力するか、削除してください");
            FocusRequested?.Invoke(this, empty);
            return false;
        }

        try
        {
            await focusTriggers.SaveAsync(Items.Select(item => new CliFocusTrigger { Text = item.Text, MoveToTerminal = item.MoveToTerminal }));
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return false;
        }
        return true;
    }

    /// <summary>編集中の項目を作る</summary>
    /// <param name="text">出力に出る文字列</param>
    /// <returns>編集中の項目</returns>
    private FocusTriggerEditItem CreateItem(string text) => new(RemoveItem) { Text = text };

    /// <summary>項目を一覧から取り除く</summary>
    /// <param name="item">取り除く項目</param>
    private void RemoveItem(FocusTriggerEditItem item)
    {
        Items.Remove(item);
        OnPropertyChanged(nameof(HasNoItems));
    }
}
