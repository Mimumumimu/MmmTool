using System.Globalization;
using System.Text;

namespace MmmTool.Notes.Core;

/// <summary>
/// その日のノートを、カードに並べた 1 枚の HTML のページにする。
/// </summary>
/// <param name="formatter">本文を、表示してよい HTML にする処理</param>
/// <remarks>
/// ページはスクリプトを使わない (表示側が、スクリプトを止める)。「タイトルだけ」でカードを開く動きは <c>details</c>、「編集」「削除」「復元」は内部リンク (<see cref="NoteLinks.ActionUrl"/>)。
/// タイトル・名前・本文は、すべて HTML の文字に直すか、<see cref="INoteBodyFormatter"/> が組み立てたものだけを出す。外部の資源は読み込まない。
/// 色は、ライト・ダークのそれぞれの値を CSS の変数に持ち、ページの <c>html</c> 要素のクラス (<c>dark</c>)で切り替える。
/// </remarks>
public sealed class NotePageBuilder(INoteBodyFormatter formatter)
{
    /// <summary>曜日の表示 (日曜が先頭。PC の言語に依らず日本語)</summary>
    private static readonly string[] Weekdays = ["日", "月", "火", "水", "木", "金", "土"];

    /// <summary>ページを作る</summary>
    /// <param name="cards">カードに出す内容 (並べ終えたもの)</param>
    /// <param name="options">条件</param>
    /// <param name="frames">カードの上に出す、他の機能の内容の枠 (無ければ null)</param>
    /// <returns>HTML のページ</returns>
    /// <remarks>
    /// 枠は、ノートの番号と重ならないよう、負の番号 (<c>n-1</c> から)の id を付け、カードと同じ開閉の決まり (<see cref="NotePageOptions.OpenIds"/>)で出す。
    /// 枠の文は、すべて HTML の文字に直して出す。
    /// </remarks>
    public string Build(IReadOnlyList<NoteCard> cards, NotePageOptions options, IReadOnlyList<DayContentFrame>? frames = null)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(options);

        var html = new StringBuilder(4096);
        html.Append("<!DOCTYPE html>\n<html lang=\"ja\" class=\"").Append(options.IsDark ? "dark" : "light").Append("\">\n<head>\n");
        html.Append("<meta charset=\"utf-8\">\n<meta name=\"color-scheme\" content=\"").Append(options.IsDark ? "dark" : "light").Append("\">\n");
        html.Append("<title>ノート</title>\n<style>\n").Append(Css()).Append("</style>\n</head>\n<body>\n");

        for (var i = 0; i < (frames?.Count ?? 0); i++)
        {
            AppendFrame(html, frames![i], -(i + 1), options);
        }

        if (cards.Count == 0)
        {
            html.Append("<p class=\"empty\"><span class=\"empty-date\">").Append(Escape(FormatDay(options.Day))).Append("</span>この日のノートはありません</p>\n");
        }
        foreach (var card in cards)
        {
            AppendCard(html, card, options);
        }

        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    /// <summary>他の機能の内容の枠 1 つを出力に出す</summary>
    /// <param name="html">出力</param>
    /// <param name="frame">枠の内容</param>
    /// <param name="id">枠の番号で、負の数。カードの開閉の状態と同じ仕組みで覚える</param>
    /// <param name="options">条件</param>
    private static void AppendFrame(StringBuilder html, DayContentFrame frame, int id, NotePageOptions options)
    {
        var isKnown = options.OpenIds is not null && options.KnownIds?.Contains(id) == true;
        var isOpen = isKnown ? options.OpenIds!.Contains(id) : !options.TitleOnly;
        html.Append("<section class=\"frame\" id=\"n").Append(id.ToString(CultureInfo.InvariantCulture)).Append("\">\n<details").Append(isOpen ? " open" : "").Append(">\n");
        html.Append("<summary><span class=\"title\">").Append(Escape(frame.Title)).Append("</span></summary>\n");
        html.Append("<pre class=\"text\">").Append(Escape(frame.Text)).Append("</pre>\n</details>\n</section>\n");
    }

    /// <summary>カード 1 枚を出力に出す</summary>
    /// <param name="html">出力</param>
    /// <param name="card">カードに出す内容</param>
    /// <param name="options">条件</param>
    private void AppendCard(StringBuilder html, NoteCard card, NotePageOptions options)
    {
        var note = card.Note;
        var isKnown = options.OpenIds is not null && options.KnownIds?.Contains(note.Id) == true;
        var isOpen = isKnown ? options.OpenIds!.Contains(note.Id) : !options.TitleOnly;

        html.Append("<article class=\"card p").Append((int)note.Priority).Append(card.IsEventDay ? " event" : "").Append(note.IsDeleted ? " deleted" : "").Append("\" id=\"n").Append(note.Id.ToString(CultureInfo.InvariantCulture)).Append("\">\n");

        // 実施日の帯は、カードの上端に置く。閉じていても見える
        if (card.IsEventDay)
        {
            html.Append("<div class=\"event-band\">").Append(card.IsEventToday ? "今日が実施日" : "この日が実施日").Append("</div>\n");
        }

        // 操作 (カードの右上)
        html.Append("<div class=\"actions\">");
        if (note.IsDeleted)
        {
            AppendAction(html, NoteAction.Restore, note.Id, "復元");
        }
        else
        {
            AppendAction(html, NoteAction.Edit, note.Id, "編集");
            AppendAction(html, NoteAction.Delete, note.Id, "削除");
        }
        html.Append("</div>\n");

        // 本文が無いカードは、開く先が無いので、開閉の矢印を出さない
        var hasBody = !string.IsNullOrWhiteSpace(note.Body);
        if (hasBody)
        {
            html.Append("<details").Append(isOpen ? " open" : "").Append(">\n<summary>\n");
        }
        else
        {
            html.Append("<div class=\"head\">\n");
        }
        // 印は、タイトルの右に同じ行で出す (印のために行を増やさない)
        html.Append("<span class=\"title\">").Append(Escape(note.Title)).Append("</span>");
        AppendBadges(html, card, options.IsShared);
        html.Append("<span class=\"meta\">");
        foreach (var part in Meta(card, options.IsShared, options.Day))
        {
            html.Append("<span>").Append(Escape(part)).Append("</span>");
        }
        html.Append("</span>\n");
        if (hasBody)
        {
            html.Append("</summary>\n");
            html.Append("<div class=\"body\">").Append(formatter.ToHtml(note.Body)).Append("</div>\n");
            html.Append("</details>\n</article>\n");
        }
        else
        {
            html.Append("</div>\n</article>\n");
        }
    }

    /// <summary>カードの操作の内部リンクを出力に出す</summary>
    /// <param name="html">出力</param>
    /// <param name="action">操作</param>
    /// <param name="noteId">ノートの番号</param>
    /// <param name="label">リンクの文字</param>
    private static void AppendAction(StringBuilder html, NoteAction action, int noteId, string label)
        => html.Append("<a class=\"action\" href=\"").Append(NoteLinks.ActionUrl(action, noteId)).Append("\">").Append(label).Append("</a>");

    /// <summary>印 (今日の更新・自分だけ・削除済み)を出力に出す</summary>
    /// <param name="html">出力</param>
    /// <param name="card">カードに出す内容</param>
    /// <param name="isShared">複数の人で共有するか</param>
    private static void AppendBadges(StringBuilder html, NoteCard card, bool isShared)
    {
        var note = card.Note;
        if (!card.IsUpdatedToday && !note.IsDeleted && !(isShared && note.IsPrivate))
        {
            return;
        }

        html.Append("<span class=\"badges\">");
        if (card.IsUpdatedToday)
        {
            html.Append("<span class=\"badge today\">今日更新</span>");
        }
        if (isShared && note.IsPrivate)
        {
            html.Append("<span class=\"badge\">自分だけ</span>");
        }
        if (note.IsDeleted)
        {
            html.Append("<span class=\"badge\">削除済み</span>");
        }
        html.Append("</span>\n");
    }

    /// <summary>カードの上の行に出す、期間・実施日・作成者・更新者・更新日時の文字を作る</summary>
    /// <param name="card">カードに出す内容</param>
    /// <param name="isShared">複数の人で共有するか</param>
    /// <param name="day">表示する日</param>
    /// <returns>項目ごとの文字。期間・実施日・作成者・更新の順で、項目の間の区切りは CSS が付ける</returns>
    /// <remarks>実施日は、設定があり、表示する日より後のときだけ小さく出す。当日は帯で示し、過ぎたあとは出さない。同じ年は、年を省く。</remarks>
    private static List<string> Meta(NoteCard card, bool isShared, DateOnly day)
    {
        var note = card.Note;
        var parts = new List<string>
        {
            note.IsOpenEnded ? $"{FormatDate(note.StartDate)} ～" : $"{FormatDate(note.StartDate)} ～ {FormatDate(note.EndDate)}",
        };
        if (note.HasEventDate && note.EventDate > day)
        {
            var text = note.EventDate.Year == day.Year
                ? note.EventDate.ToString("MM/dd", CultureInfo.InvariantCulture)
                : FormatDate(note.EventDate);
            parts.Add($"実施 {text}");
        }
        if (isShared)
        {
            parts.Add($"作成 {card.AuthorName}");
        }

        var updated = note.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
        parts.Add(isShared && card.IsUpdatedByOther ? $"更新 {card.UpdatedByName} {updated}" : $"更新 {updated}");
        return parts;
    }

    /// <summary>日付を <c>yyyy/MM/dd</c> にする</summary>
    /// <param name="date">日付</param>
    /// <returns>文字</returns>
    private static string FormatDate(DateOnly date) => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    /// <summary>日付を、曜日つき (<c>yyyy/MM/dd (土)</c>)にする</summary>
    /// <param name="date">日付</param>
    /// <returns>文字</returns>
    private static string FormatDay(DateOnly date) => $"{FormatDate(date)} ({Weekdays[(int)date.DayOfWeek]})";

    /// <summary>文字を、HTML の文字に直す</summary>
    /// <param name="text">文字</param>
    /// <returns>直した文字列</returns>
    private static string Escape(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>ページの CSS (パレットの色のクラスを含む)</summary>
    /// <returns>CSS</returns>
    private static string Css()
    {
        var css = new StringBuilder(8192);
        css.Append(
            """
            :root { --text:#1B1B1B; --sub:#5B5B5B; --card:#FFFFFF; --border:#D6D6D6; --link:#0F6CBD; --p5:#C42B1C; --p4:#D9730D; --badge:#EAEAEA; --badge-text:#303030; --today:#FDE7E9; --today-text:#8E1B10; --event:#6F2DA8; --event-text:#FFFFFF; --event-bg:#F3E8FB; --event-glow:rgba(111,45,168,0.55); --event-glow-peak:rgba(111,45,168,0.9); }
            html.dark { --text:#F0F0F0; --sub:#BDBDBD; --card:#2B2B2B; --border:#454545; --link:#6CB8F6; --p5:#FF7B6E; --p4:#FFB454; --badge:#3A3A3A; --badge-text:#E6E6E6; --today:#5A2A28; --today-text:#FFD0CB; --event:#B58CF0; --event-text:#1A1030; --event-bg:#3A2D4D; --event-glow:rgba(181,140,240,0.5); --event-glow-peak:rgba(181,140,240,0.9); }
            * { box-sizing:border-box; }
            html, body { margin:0; background:transparent; color:var(--text); }
            body { padding:4px 20px 24px 4px; font:14px/1.7 "BIZ UDGothic","BIZ UDゴシック","MS Gothic","ＭＳ ゴシック",Consolas,monospace; }
            a { color:var(--link); }
            .empty { margin:16px 0 0; padding-left:16px; text-align:center; color:var(--sub); }
            .empty-date { display:block; margin-bottom:4px; font-size:1.1em; font-weight:600; color:var(--text); }
            .frame { margin:0 0 8px; background:var(--card); border:1px solid var(--border); border-radius:8px; padding:8px 16px 8px 19px; }
            .frame summary { cursor:pointer; list-style:none; }
            .frame .text { margin:6px 0 2px 18px; font:inherit; line-height:22px; white-space:pre-wrap; overflow-wrap:anywhere; user-select:text; }
            .card { position:relative; margin:0 0 8px; background:var(--card); border:1px solid var(--border); border-radius:8px; padding:8px 16px 8px 19px; }
            .card.p5, .card.p4 { border-left-width:4px; padding-left:16px; }
            .card.p5 { border-left-color:var(--p5); }
            .card.p4 { border-left-color:var(--p4); }
            .card.event { --pl:18px; padding:0 15px 7px 18px; background:var(--event-bg); border:2px solid var(--event); box-shadow:0 0 10px 1px var(--event-glow); animation:event-pulse 1.2s ease-in-out 3; }
            .card.event.p5, .card.event.p4 { --pl:16px; padding-left:16px; border-left-width:4px; }
            .card.event.p5 { border-left-color:var(--p5); }
            .card.event.p4 { border-left-color:var(--p4); }
            .event-band { margin:0 -15px 8px calc(-1 * var(--pl)); padding:2px 16px; border-radius:6px 6px 0 0; background:var(--event); color:var(--event-text); font-size:13px; font-weight:700; line-height:22px; }
            .card.event .actions { top:34px; }
            .card.event .title { font-size:19px; }
            @keyframes event-pulse { 0%, 100% { box-shadow:0 0 10px 1px var(--event-glow); } 50% { box-shadow:0 0 20px 5px var(--event-glow-peak); } }
            @media (prefers-reduced-motion: reduce) { .card.event { animation:none; } }
            .card.deleted { opacity:0.6; }
            .card.deleted .title { text-decoration:line-through; }
            .actions { position:absolute; top:8px; right:14px; display:flex; gap:12px; line-height:1.7; }
            .action { font-size:13px; text-decoration:none; }
            .action:hover { text-decoration:underline; }
            summary, .head { display:flex; flex-wrap:wrap; align-items:baseline; padding-right:96px; }
            summary { cursor:pointer; list-style:none; }
            .head > .title { margin-left:18px; max-width:calc(100% - 18px); }
            summary > .title { max-width:calc(100% - 22px); }
            summary::-webkit-details-marker { display:none; }
            .frame summary > .title { max-width:100%; }
            summary::before { content:"\203A"; display:inline-block; width:12px; margin:0 10px 0 -4px; text-align:center; color:var(--sub); transition:transform 0.1s; }
            details[open] > summary::before { transform:rotate(90deg); }
            .title { font-size:16px; font-weight:600; }
            .meta { margin-left:auto; padding-left:16px; font-size:12px; color:var(--sub); }
            .meta > span + span { margin-left:8px; padding-left:8px; border-left:1px solid var(--border); }
            .badges { margin-left:4px; }
            .badge { display:inline-block; margin-left:6px; vertical-align:2px; padding:0 8px; border-radius:10px; font-size:12px; line-height:20px; background:var(--badge); color:var(--badge-text); }
            .badge.today { background:var(--today); color:var(--today-text); }
            .body { margin:6px 0 2px 18px; overflow-wrap:anywhere; }
            .body ul, .body ol { margin:4px 0; padding-left:24px; }

            """);

        foreach (var color in NoteColorPalette.All)
        {
            css.Append(".nc").Append(color.Index.ToString(CultureInfo.InvariantCulture)).Append(" { color:").Append(color.Hex).Append("; }\n");
        }
        foreach (var color in NoteColorPalette.All)
        {
            css.Append("html.dark .nc").Append(color.Index.ToString(CultureInfo.InvariantCulture)).Append(" { color:").Append(color.DarkHex).Append("; }\n");
        }
        return css.ToString();
    }
}
