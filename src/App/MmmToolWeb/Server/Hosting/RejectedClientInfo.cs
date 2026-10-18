namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 断った接続元 1 つぶんの記録。
/// </summary>
/// <param name="Address">接続元。IPv6 は前半の 64 ビットで 1 つとみなした形。</param>
/// <param name="FirstAt">最初に断った日時。</param>
/// <param name="LastAt">最後に断った日時。</param>
/// <param name="Count">断った回数。</param>
/// <param name="LastPath">最後に要求した道。</param>
internal sealed record RejectedClientInfo(string Address, DateTimeOffset FirstAt, DateTimeOffset LastAt, int Count, string LastPath);
