using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Data.Connection;
using MmmTool.Features.Database.Connection;

namespace MmmTool.Features.Database.Settings;

/// <summary>
/// 保存先 (ローカル / DB)と、DB への接続の設定 (設定ページの部品)の ViewModel。
/// </summary>
/// <remarks>保存するのは「保存」を押したときだけ。保存先は起動時に決まって使い始めるので、変更は次の起動から反映する。</remarks>
public sealed partial class DatabaseSettingsViewModel : ObservableObject
{
    /// <summary>ViewModel を作り、保存済みの保存先を読み込む</summary>
    /// <param name="connection">接続の入力欄</param>
    /// <param name="settings">接続の設定の読み書き</param>
    public DatabaseSettingsViewModel(DatabaseConnectionViewModel connection, DatabaseSettingsService settings)
    {
        Connection = connection;
        IsEditable = !settings.IsReadOnly;
        Modes =
        [
            new DatabaseModeOption(DatabaseMode.Json, "ローカル (この PC だけで使う)"),
            new DatabaseModeOption(DatabaseMode.SqlServer, "DB (複数の PC で共有する)"),
        ];
        SelectedMode = Modes.First(option => option.Value == settings.Load().Mode);
    }

    /// <summary>接続の入力欄 (保存先が DB のときに出す)</summary>
    public DatabaseConnectionViewModel Connection { get; }

    /// <summary>設定を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>保存先の選択肢</summary>
    public IReadOnlyList<DatabaseModeOption> Modes { get; }

    /// <summary>選んでいる保存先</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSqlServer))]
    public partial DatabaseModeOption? SelectedMode { get; set; }

    /// <summary>保存先が DB か (接続の入力欄を出す)</summary>
    public bool IsSqlServer => SelectedMode is { Value: DatabaseMode.SqlServer };

    /// <summary>保存先と接続の設定を保存する</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存できなかったとき (入力が足りない・設定ファイルを読めなかった)は、入力欄のエラーに出す。</remarks>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedMode is { } mode && await Connection.SaveAsync(mode.Value))
        {
            Connection.Success.Show("保存しました。");
        }
    }
}
