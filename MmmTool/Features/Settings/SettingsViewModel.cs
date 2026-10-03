using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Settings;
using MmmSdk.WinUI.Errors;

namespace MmmTool.Features.Settings;

/// <summary>設定ページの ViewModel</summary>
/// <remarks>各機能の設定の値と画面の状態は、機能ごとの部品（<see cref="MmmTool.Shell.SettingsSection"/>）が持つ。ここは、ページ全体のこと（タイトル・設定ファイルを読めなかったときの知らせ）だけ。</remarks>
public sealed class SettingsViewModel : ObservableObject
{
    /// <summary>ページのタイトル</summary>
    public string Title => "設定";

    /// <summary>画面に出すエラー（設定ファイルを読めなかったとき）</summary>
    public ErrorState Error { get; } = new();

    /// <summary>ページを作る</summary>
    /// <param name="settings">汎用設定ストア</param>
    /// <remarks>設定ファイルを読めなかったときは、各部品が上書きして消さないよう、変更を止める（部品は、そのサービスの <c>IsReadOnly</c> で入力欄を無効にする）。</remarks>
    public SettingsViewModel(ISettingsStore settings)
    {
        if (settings.LoadError is { } loadError)
        {
            Error.Show($"{loadError}\n設定を読み込めなかったため、変更を保存できません。");
        }
    }
}
