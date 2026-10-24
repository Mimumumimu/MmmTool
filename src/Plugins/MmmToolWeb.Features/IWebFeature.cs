using System.Reflection;

namespace MmmToolWeb.Features;

/// <summary>
/// Web 版の機能 (ノート・リマインダーなど)のサーバー側の入口。サーバー本体は、機能の名前を持たず、この入口だけを知る。
/// </summary>
public interface IWebFeature
{
    /// <summary>
    /// この機能の画面 (ブラウザ側)のアセンブリ。
    /// </summary>
    /// <remarks>画面の道 (<c>@page</c>)を、ここから集める。探りを断る処理が、画面の道を「存在しない道」と取り違えないため。</remarks>
    Assembly ClientAssembly { get; }

    /// <summary>
    /// 管理者が、ユーザーごとに利用を決められる機能の機能番号で、<c>CodeFeatures</c> の番号。決められない機能は null。
    /// </summary>
    /// <remarks>
    /// 番号がある機能は、管理者に無効にされたユーザーに対して、API を 404 で断る。ホーム画面と設定の画面にも出さない。
    /// 断る処理は、機能ごとに書かず、API の入口の <see cref="WebFeatureExtensions.MapWebFeatures"/> が行う。
    /// </remarks>
    int? FeatureNo => null;

    /// <summary>
    /// この機能が使うサービスを登録する。
    /// </summary>
    /// <param name="services">登録先。</param>
    void AddServices(IServiceCollection services);

    /// <summary>
    /// この機能の API を登録する。
    /// </summary>
    /// <param name="api">API の入口 <c>/api</c>。ログインしている人だけが呼べる。機能は、この下に自分の道を足す。</param>
    void MapEndpoints(RouteGroupBuilder api);
}
