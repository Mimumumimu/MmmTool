namespace MmmTool.Messages.Core;

/// <summary>システムメッセージの決まりと、文の作り方</summary>
/// <remarks>
/// 人の追加・抜けた・名前の変更を、会話の中に残す 1 通のメッセージ。<c>Message.SenderUserId</c> を 0 にして書く。
/// 本文には出来上がった文を入れる。名前はその時点の表示名で固まり、あとで表示名が変わっても、過去の文は変えない。
/// </remarks>
public static class SystemMessage
{
    /// <summary>システムメッセージの <c>Message.SenderUserId</c>。ユーザーではないことを表す</summary>
    public const int SenderUserId = 0;

    /// <summary>人を追加したときの文を作る</summary>
    /// <param name="actorName">追加した人の表示名</param>
    /// <param name="addedNames">追加された人の表示名</param>
    /// <returns>「○○さんが△△さん・□□さんを追加しました」</returns>
    public static string Added(string actorName, IEnumerable<string> addedNames)
    {
        ArgumentNullException.ThrowIfNull(actorName);
        ArgumentNullException.ThrowIfNull(addedNames);

        return $"{actorName}さんが{string.Join("・", addedNames.Select(name => name + "さん"))}を追加しました";
    }

    /// <summary>グループを抜けたときの文を作る</summary>
    /// <param name="actorName">抜けた人の表示名</param>
    /// <returns>「○○さんが抜けました」</returns>
    public static string Left(string actorName)
    {
        ArgumentNullException.ThrowIfNull(actorName);

        return $"{actorName}さんが抜けました";
    }

    /// <summary>グループの名前を変えたときの文を作る</summary>
    /// <param name="actorName">変えた人の表示名</param>
    /// <param name="newName">新しい名前</param>
    /// <returns>「○○さんがグループ名を『新しい名前』に変更しました」</returns>
    public static string Renamed(string actorName, string newName)
    {
        ArgumentNullException.ThrowIfNull(actorName);
        ArgumentNullException.ThrowIfNull(newName);

        return $"{actorName}さんがグループ名を『{newName}』に変更しました";
    }
}
