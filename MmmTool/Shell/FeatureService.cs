using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Settings;

namespace MmmTool.Shell;

/// <summary>機能のオン・オフの管理（状態の保存・起動時の準備の実行・切り替えの通知）</summary>
/// <param name="features">オン・オフを切り替えられる機能</param>
/// <param name="confirmations">オフにする前の確認</param>
/// <param name="startups">起動時の準備</param>
/// <param name="settings">汎用設定ストア</param>
/// <param name="services">起動時の準備を作る DI のサービスプロバイダー</param>
/// <remarks>
/// 状態は設定ストアに、機能のキーごとに保存する。保存が無い機能はオン（機能を足しても、移行は要らない）。
/// 切り替えはすぐ反映する。サイドバー・トレイ・設定ページは、<see cref="Changed"/> または <see cref="IsEnabled"/> で見る。
/// 機能の登録（<see cref="ShellServiceCollectionExtensions.AddFeature"/> 等）は各機能が行い、このクラスは特定の機能を知らない。
/// </remarks>
public sealed class FeatureService(
    IEnumerable<FeatureInfo> features,
    IEnumerable<IFeatureDisableConfirmation> confirmations,
    IEnumerable<StartupTaskRegistration> startups,
    ISettingsStore settings,
    IServiceProvider services)
{
    /// <summary>オン・オフを切り替えられる機能（登録順）</summary>
    public IReadOnlyList<FeatureInfo> Features { get; } = [.. features];

    /// <summary>機能のオン・オフが切り替わったとき（引数は、切り替わった機能のキー）</summary>
    public event EventHandler<string>? Changed;

    /// <summary>機能がオンか</summary>
    /// <param name="featureKey">機能のキー。null（オフにできない機能）なら常にオン</param>
    /// <returns>オンなら true。保存が無いときもオン</returns>
    public bool IsEnabled(string? featureKey)
        => featureKey is null || settings.Get(SettingKey(featureKey), true);

    /// <summary>オンの機能の起動時の準備を、登録順に実行する</summary>
    /// <returns>準備の完了を表すタスク</returns>
    /// <remarks>
    /// 画面を作る前に、UI スレッドで行う。共通の設定ファイルの先読み（<see cref="SettingsStoreStartup"/>）が最初に登録されているので、
    /// 機能のオン・オフの判定は、読み込み済みの設定で行われる。
    /// </remarks>
    public async Task StartAsync()
    {
        foreach (var startup in startups.Where(s => IsEnabled(s.FeatureKey)))
        {
            await RunAsync(startup);
        }
    }

    /// <summary>機能をオン・オフする</summary>
    /// <param name="featureKey">機能のキー</param>
    /// <param name="enabled">オンにするなら true</param>
    /// <returns>切り替えた結果</returns>
    /// <remarks>
    /// オフにするときは、先に確認（<see cref="IFeatureDisableConfirmation"/>）を取る。
    /// オンにするときは、その機能の起動時の準備を実行してから <see cref="Changed"/> を出す。
    /// オフにするときは、保存してから <see cref="Changed"/> を出す（受け取った側がページを捨てる）。
    /// </remarks>
    public async Task<FeatureChangeResult> SetEnabledAsync(string featureKey, bool enabled)
    {
        if (IsEnabled(featureKey) == enabled)
        {
            return FeatureChangeResult.Changed;
        }

        if (!enabled
            && confirmations.FirstOrDefault(c => c.FeatureKey == featureKey) is { } confirmation
            && !await confirmation.ConfirmAsync())
        {
            return FeatureChangeResult.Cancelled;
        }

        if (!await settings.SetAsync(SettingKey(featureKey), enabled))
        {
            return FeatureChangeResult.NotSaved;
        }

        if (enabled)
        {
            foreach (var startup in startups.Where(s => s.FeatureKey == featureKey))
            {
                await RunAsync(startup);
            }
        }

        Changed?.Invoke(this, featureKey);
        return FeatureChangeResult.Changed;
    }

    /// <summary>起動時の準備を 1 つ実行する</summary>
    /// <param name="startup">起動時の準備の登録情報</param>
    /// <returns>準備の完了を表すタスク</returns>
    private Task RunAsync(StartupTaskRegistration startup)
        => ((IStartupTask)services.GetRequiredService(startup.TaskType)).StartAsync();

    /// <summary>機能のオン・オフを保存する、設定ストアのキー</summary>
    /// <param name="featureKey">機能のキー</param>
    /// <returns>設定ストアのキー</returns>
    private static string SettingKey(string featureKey) => $"Feature.{featureKey}.Enabled";
}
