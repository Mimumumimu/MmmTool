namespace MmmTool.Shell;

/// <summary>起動時の準備の登録情報</summary>
/// <param name="TaskType">起動時の準備の型 (<see cref="IStartupTask"/> を実装し、DI から取る)</param>
/// <param name="FeatureKey">属する機能のキー。オフにできない機能・共通の準備は null</param>
/// <remarks><see cref="ShellServiceCollectionExtensions.AddStartupTask{TTask}"/> が登録し、<see cref="FeatureService"/> が、オンの機能のものだけを登録順に実行する。</remarks>
public sealed record StartupTaskRegistration(Type TaskType, string? FeatureKey);
