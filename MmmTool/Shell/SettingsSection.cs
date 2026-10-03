namespace MmmTool.Shell;

/// <summary>設定ページに並べる、機能ごとの設定の部品（登録情報）</summary>
/// <param name="ControlType">設定の部品（<c>UserControl</c>）の型</param>
/// <remarks>各機能が <see cref="ShellServiceCollectionExtensions.AddSettingsSection{TControl}"/> で登録し、設定ページが登録順に並べる（サイドバーの <see cref="NavigationPage"/> と同じ形）。</remarks>
public sealed record SettingsSection(Type ControlType);
