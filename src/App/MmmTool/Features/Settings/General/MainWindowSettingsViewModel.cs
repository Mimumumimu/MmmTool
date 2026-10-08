using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Shell;

namespace MmmTool.Features.Settings.General;

/// <summary>メインウィンドウの設定 (設定ページの部品)の ViewModel</summary>
public sealed partial class MainWindowSettingsViewModel : ObservableObject
{
    /// <summary>メインウィンドウの設定</summary>
    private readonly MainWindowSettingsService _settings;

    /// <summary>保存済みの値を入れている最中か</summary>
    /// <remarks>true の間は、値の変更で保存しない (読み込んだだけの値で、設定ファイルを書き換えないため)。</remarks>
    private bool _isInitializing;

    /// <summary>画面に出すエラー (保存に失敗したとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>設定を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>起動時にメイン画面を開くか</summary>
    /// <remarks>変わったら即保存する。</remarks>
    [ObservableProperty]
    public partial bool OpenOnStartup { get; set; }

    /// <summary>保存済みの値を読み込んで表示する</summary>
    /// <param name="settings">メインウィンドウの設定</param>
    public MainWindowSettingsViewModel(MainWindowSettingsService settings)
    {
        _settings = settings;
        _isInitializing = true;
        OpenOnStartup = settings.OpenOnStartup;
        _isInitializing = false;
        IsEditable = !settings.IsReadOnly;
    }

    /// <summary>値が変わったら保存する</summary>
    /// <param name="value">変更後の値</param>
    partial void OnOpenOnStartupChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }
        SaveAsync(value).Forget();
    }

    /// <summary>起動時にメイン画面を開くかを保存する</summary>
    /// <param name="value">保存する値</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>
    /// 保存できない状態 (設定ファイルを読めなかった)では、スイッチを無効にしてあるので、ここへは来ない (来たら、保存されなかったことを知らせる)。
    /// 保存の失敗 (ロック・権限など)は、画面に出す。
    /// </remarks>
    private async Task SaveAsync(bool value)
    {
        try
        {
            if (await _settings.SetOpenOnStartupAsync(value))
            {
                Error.Clear();
            }
            else
            {
                Error.Show("設定を読み込めなかったため、変更を保存できませんでした。");
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }
}
