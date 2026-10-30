using Microsoft.Extensions.DependencyInjection;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notes.Client;

/// <summary>
/// ノートの画面を、ブラウザ側のアプリに足す。
/// </summary>
public static class NotesClientServiceCollectionExtensions
{
    /// <summary>アイコンの形。書類の絵。</summary>
    private const string IconPath = "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z";

    /// <summary>
    /// ノートの画面と、API を呼ぶ部品を登録する。
    /// </summary>
    /// <param name="services">登録先。</param>
    /// <returns>続けて呼ぶための <paramref name="services"/>。</returns>
    public static IServiceCollection AddNotesClient(this IServiceCollection services)
    {
        services.AddScoped<NotesClient>();
        return services.AddClientFeature(new ClientFeature("ノート", IconPath, NotesPage.Route, typeof(NotesPage).Assembly, UnreadKind: NotesPage.NoteNoticeKind));
    }
}
