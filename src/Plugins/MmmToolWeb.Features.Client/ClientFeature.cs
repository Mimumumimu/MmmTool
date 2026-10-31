using System.Reflection;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// 画面 (ブラウザ側)に足した機能 1 つ。画面の本体は、機能の名前を持たず、この登録だけを知る。
/// </summary>
/// <param name="Title">機能の名前。ホーム画面のタイルと、設定の画面の節の見出しに出す。</param>
/// <param name="IconPath">アイコンの形。24 × 24 の枠の SVG で、path の <c>d</c> 属性の値。</param>
/// <param name="Route">この機能の最初の画面の道。</param>
/// <param name="Assembly">この機能の画面 (<c>@page</c>)を持つアセンブリ。ルーターに足す。</param>
/// <param name="SettingsSection">設定の画面に出す、この機能の節の部品の型。設定を持たない機能は null。</param>
/// <param name="MoreSettingsSections">設定の画面に出す、この機能の節をさらに足すときの、見出しと部品の型。<paramref name="SettingsSection"/> の後ろに並ぶ。足さなければ null。</param>
/// <param name="UnreadKind">ホーム画面のタイルに、読んでいない通知の数を出すときの、通知の種類。通知の識別子「種類:番号」の、頭の部分。数を出さない機能は null。同じ種類の <see cref="IUnreadCountSource"/> があれば、数はサーバーに聞く。無ければ、端末の控えの一覧から数える。</param>
/// <remarks>
/// 登録した順に、ホーム画面のタイルと、設定の画面の節が並ぶ。
/// 設定の画面は、機能の名前を持たず、登録された節を並べるだけ。
/// </remarks>
public sealed record ClientFeature(
    string Title, string IconPath, string Route, Assembly Assembly, Type? SettingsSection = null, IReadOnlyList<ClientSettingsSection>? MoreSettingsSections = null, string? UnreadKind = null);

/// <summary>
/// 設定の画面に出す節 1 つ。機能の名前の節とは別に、見出しを持つ。
/// </summary>
/// <param name="Title">節の見出し。</param>
/// <param name="Component">節の部品の型。</param>
public sealed record ClientSettingsSection(string Title, Type Component);
