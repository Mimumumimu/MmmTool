using System.Reflection;

namespace MmmToolWeb.Features;

/// <summary>Web 版の機能のサーバー側の入口</summary>
/// <remarks>サーバー本体は機能の名前を持たず、この入口だけを知る。</remarks>
public interface IWebFeature
{
    /// <summary>この機能の画面のアセンブリ</summary>
    /// <remarks>
    /// 画面の道 (<c>@page</c>)をここから集める
    /// 探りを断る処理が画面の道を「存在しない道」と取り違えないため
    /// </remarks>
    Assembly ClientAssembly { get; }

    /// <summary>管理者がユーザーごとに利用を決められる機能の機能番号で、CodeFeatures の番号</summary>
    /// <remarks>
    /// 決められない機能は null。
    /// 番号がある機能は管理者に無効にされたユーザーに対して、API を 404 で断る。
    /// ホーム画面と設定の画面にも出さない。
    /// 断る処理は機能ごとに書かず、API の入口の <see cref="WebFeatureExtensions.MapWebFeatures"/> が行う。
    /// </remarks>
    int? FeatureNo => null;

    /// <summary>この機能が使うサービスを登録する</summary>
    /// <param name="services">登録先</param>
    void AddServices(IServiceCollection services);

    /// <summary>この機能の API を登録する</summary>
    /// <param name="api">
    /// API の入口 <c>/api</c>
    /// ログインしている人だけが呼べる
    /// 機能はこの下に自分の道を足す
    /// </param>
    void MapEndpoints(RouteGroupBuilder api);
}
