using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing.Template;
using MmmToolWeb.Client.Auth;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// ブラウザ側 (Client)の画面の道の一覧。画面ごとの <c>@page</c> から集めるので、画面を足しても、ここは直さない。
/// </summary>
internal sealed class ClientRoutes
{
    /// <summary>画面の道ごとの照合。</summary>
    private readonly List<TemplateMatcher> _matchers = typeof(MeResponse).Assembly.GetTypes()
        .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false))
        .Select(route => new TemplateMatcher(TemplateParser.Parse(route.Template), new RouteValueDictionary()))
        .ToList();

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
