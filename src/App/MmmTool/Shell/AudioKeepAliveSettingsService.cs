using MmmSdk.Core.Components.Settings;
using MmmSdk.WinUI.Components.Audio;

namespace MmmTool.Shell;

/// <summary>音声機器を眠らせない無音の出力の設定</summary>
/// <param name="settings">汎用設定ストア</param>
/// <param name="keepAlive">無音の出力</param>
/// <remarks>
/// 汎用設定ストアに保存・取得し、保存と同時に無音の出力を始める・止める。
/// PC ごとの音声機器の事情なので、DB モードでも、設定ストア (この PC のファイル)に置く。
/// </remarks>
public sealed class AudioKeepAliveSettingsService(ISettingsStore settings, IAudioKeepAlive keepAlive)
{
    /// <summary>無音の出力を流すかの設定キー</summary>
    private const string EnabledKey = "Shell.AudioKeepAlive";

    /// <summary>無音の出力を流すか</summary>
    /// <remarks>無ければ流さない</remarks>
    public bool IsEnabled => settings.Get(EnabledKey, false);

    /// <summary>設定を保存できない状態か</summary>
    /// <remarks>設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている。</remarks>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>保存済みの設定に合わせて無音の出力を始める</summary>
    /// <returns>始める処理の完了を表すタスク</returns>
    /// <remarks>オンのときだけ</remarks>
    public async Task ApplyAsync()
    {
        if (IsEnabled)
        {
            await keepAlive.StartAsync();
        }
    }

    /// <summary>無音の出力を流すかを保存し、すぐに始める・止める</summary>
    /// <param name="value">流すなら true</param>
    /// <returns>
    /// 保存したら true
    /// 設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false (このときは始めも止めもしない)
    /// </returns>
    public async Task<bool> SetEnabledAsync(bool value)
    {
        if (!await settings.SetAsync(EnabledKey, value))
        {
            return false;
        }

        if (value)
        {
            await keepAlive.StartAsync();
        }
        else
        {
            await keepAlive.StopAsync();
        }
        return true;
    }
}
