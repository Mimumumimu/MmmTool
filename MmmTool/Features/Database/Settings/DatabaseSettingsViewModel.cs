using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Data.Connection;

namespace MmmTool.Features.Database.Settings;

/// <summary>
/// 保存先 (ローカル / DB)の設定ページのカードの ViewModel。今の保存先を一行で見せ、編集画面を開く。
/// </summary>
/// <remarks>保存先と接続の編集は、別の画面 (<c>DatabaseEditViewModel</c>)で行う。ここは、編集画面を閉じたあとに、見せている内容を読み直す。</remarks>
public sealed partial class DatabaseSettingsViewModel : ObservableObject
{
    /// <summary>接続の設定の読み書き</summary>
    private readonly DatabaseSettingsService _settings;

    /// <summary>編集画面を開く</summary>
    private readonly IDatabaseDialogService _dialogs;

    /// <summary>ViewModel を作り、今の保存先を読み込む</summary>
    /// <param name="settings">接続の設定の読み書き</param>
    /// <param name="dialogs">編集画面を開く</param>
    public DatabaseSettingsViewModel(DatabaseSettingsService settings, IDatabaseDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        IsEditable = !settings.IsReadOnly;
        Summary = CreateSummary();
    }

    /// <summary>設定を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>今の保存先の説明 (ローカル、または DB とサーバー名)</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; }

    /// <summary>編集画面を開き、閉じたら今の保存先を読み直す</summary>
    /// <returns>編集画面が閉じるまでの完了を表すタスク</returns>
    [RelayCommand]
    private async Task OpenAsync()
    {
        await _dialogs.ShowEditAsync();
        Summary = CreateSummary();
    }

    /// <summary>保存してある設定から、今の保存先の説明を作る</summary>
    /// <returns>ローカルなら「ローカル」、DB なら「DB ・ サーバー名」</returns>
    private string CreateSummary()
    {
        var saved = _settings.Load();
        return saved.Mode == DatabaseMode.SqlServer ? $"DB ・ {saved.Server}" : "ローカル";
    }
}
