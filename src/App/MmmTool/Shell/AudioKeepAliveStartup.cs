using MmmSdk.Core.Components.Features;

namespace MmmTool.Shell;

/// <summary>保存済みの設定がオンなら、起動時に無音の出力を始める (起動時の準備)</summary>
/// <param name="settings">無音の出力の設定</param>
/// <remarks>設定ストアの先読み (<see cref="SettingsStoreStartup"/>)のあとに実行する (先に登録する)。</remarks>
public sealed class AudioKeepAliveStartup(AudioKeepAliveSettingsService settings) : IStartupTask
{
    /// <inheritdoc />
    public Task StartAsync() => settings.ApplyAsync();
}
