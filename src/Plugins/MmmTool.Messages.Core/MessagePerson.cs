namespace MmmTool.Messages.Core;

/// <summary>
/// メッセージの宛先に探した人 1 人。
/// </summary>
/// <param name="UserId">人の番号 <c>AppUser.Id</c></param>
/// <param name="DisplayName">表示名</param>
/// <remarks>管理者にメッセージを無効にされていない人が、検索に出る。通知先の有無は問わない。送る先の値は持たない。</remarks>
public sealed record MessagePerson(int UserId, string DisplayName);
