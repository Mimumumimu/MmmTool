namespace MmmTool.ViewModels;

/// <summary>サイドバーに表示するナビゲーション項目。</summary>
/// <param name="Key">画面を識別するキー。</param>
/// <param name="Title">表示名。</param>
/// <param name="Glyph">Segoe Fluent Icons のグリフ。</param>
public sealed record NavigationItem(string Key, string Title, string Glyph);
