using MmmSdk.Core.Components.Settings;

namespace MmmTool.Shell;

/// <summary>
/// メインウィンドウの設定。汎用設定ストアに保存・取得する。
/// </summary>
/// <param name="settings">汎用設定ストア</param>
/// <remarks>
/// メインウィンドウと起動の順序はアプリ側 (<c>App</c> と <c>Shell/Main</c>)にあり、SDK にはメイン画面の考え方が無いので、アプリ側に置く。
/// DB モードでも、設定ストア (この PC のファイル)に置く。PC ごとの起動の動きで、ユーザーごとの設定ではないため。
/// </remarks>
public sealed class MainWindowSettingsService(ISettingsStore settings)
{
    /// <summary>起動時にメイン画面を開くかの設定キー</summary>
    private const string OpenOnStartupKey = "Shell.OpenMainWindowOnStartup";

    /// <summary>起動時にメイン画面を開くか (無ければ開かない。トレイアイコンだけ出す)</summary>
    public bool OpenOnStartup => settings.Get(OpenOnStartupKey, false);

    /// <summary>設定を保存できない状態か (設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている)</summary>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>起動時にメイン画面を開くかを保存する</summary>
    /// <param name="value">開くなら true</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public Task<bool> SetOpenOnStartupAsync(bool value) => settings.SetAsync(OpenOnStartupKey, value);
}
