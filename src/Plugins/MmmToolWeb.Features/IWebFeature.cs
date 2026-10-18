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
