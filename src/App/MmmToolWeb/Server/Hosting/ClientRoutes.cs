using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing.Template;
using MmmToolWeb.Client.Auth;
using MmmToolWeb.Features;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// ブラウザ側 (Client)の画面の道の一覧。本体と、足した機能の、画面の <c>@page</c> から集めるので、画面を足しても、ここは直さない。
/// </summary>
internal sealed class ClientRoutes
{
    /// <summary>画面の道ごとの照合。</summary>
    private readonly List<TemplateMatcher> _matchers;

    /// <summary>
    /// 画面の道を集める。
    /// </summary>
    /// <param name="features">足した機能。</param>
    public ClientRoutes(IEnumerable<IWebFeature> features)
    {
        _matchers = features
            .Select(feature => feature.ClientAssembly)
            .Append(typeof(MeResponse).Assembly)
            .Append(typeof(ClientFeature).Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false))
            .Select(route => new TemplateMatcher(TemplateParser.Parse(route.Template), new RouteValueDictionary()))
            .ToList();
    }

    /// <summary>
    /// 画面の道かを返す。
    /// </summary>
    /// <param name="path">要求の道。</param>
    /// <returns>画面の道のどれかに当てはまるなら true。</returns>
    public bool IsKnown(PathString path)
    {
        var values = new RouteValueDictionary();
        return _matchers.Any(matcher => matcher.TryMatch(path, values));
    }
}
