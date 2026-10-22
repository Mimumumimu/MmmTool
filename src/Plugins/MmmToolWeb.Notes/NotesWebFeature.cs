using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MmmTool.Data.SqlServer.Notes;
using MmmTool.Notes.Core;
using MmmToolWeb.Features;
using MmmToolWeb.Notes.Client;

namespace MmmToolWeb.Notes;

/// <summary>
/// ノートの Web 版のサーバー側。読み書きは、MmmTool と同じ <see cref="NoteService"/> と <c>SqlServerNoteRepository</c> を使う。
/// </summary>
public sealed class NotesWebFeature : IWebFeature
{
    /// <inheritdoc />
    public Assembly ClientAssembly => typeof(NotesClient).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // ファイルを開けない iPhone では、file: のリンクをリンクにしない
        services.AddSingleton<INoteBodyFormatter>(new NoteBodyFormatter(allowFileLinks: false));

        // 保存先とサービスは、要求ごとに作る。今のユーザーが、要求ごとに違うため
        services.AddScoped<INoteRepository, SqlServerNoteRepository>();
        services.AddScoped<NoteService>();

        // 他の機能が登録した内容 (作業報告など)を集める。提供する機能の名前は持たない
        services.AddScoped<DayContentService>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, NotesJsonContext.Default));
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api) => NoteEndpoints.Map(api.MapGroup("/notes"));
}
