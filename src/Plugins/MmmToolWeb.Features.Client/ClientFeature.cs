using System.Reflection;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// 画面 (ブラウザ側)に足した機能 1 つ。画面の本体は、機能の名前を持たず、この登録だけを知る。
/// </summary>
/// <param name="Title">機能の名前。ホーム画面のタイルに出す。</param>
/// <param name="IconPath">アイコンの形。24 × 24 の枠の SVG で、path の <c>d</c> 属性の値。</param>
/// <param name="Route">この機能の最初の画面の道。</param>
/// <param name="Assembly">この機能の画面 (<c>@page</c>)を持つアセンブリ。ルーターに足す。</param>
/// <remarks>
/// 登録した順に、ホーム画面のタイルが並ぶ。
/// </remarks>
public sealed record ClientFeature(string Title, string IconPath, string Route, Assembly Assembly);
