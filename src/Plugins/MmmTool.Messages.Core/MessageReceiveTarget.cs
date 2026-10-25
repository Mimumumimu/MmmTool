using MmmTool.CodeMaster.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Messages.Core;

/// <summary>
/// メッセージの受け取り先。ユーザーごとの設定 <c>AppUserSetting</c> の 1 行で、値が送信先の番号。
/// </summary>
/// <remarks>送信先の設定画面は、<see cref="IReceiveTargetSetting"/> として登録されたこの設定を読んで、「受け取り先」の節を作る。</remarks>
public sealed class MessageReceiveTarget : IReceiveTargetSetting
{
    /// <summary>区分番号。メッセージの受け取り先</summary>
    public const int Category = 1;

    /// <summary>コード番号。1 人に 1 つの受け取り先は 1</summary>
    public const int Code = 1;

    /// <inheritdoc />
    public int FeatureNo => CodeFeatures.Messages;

    /// <inheritdoc />
    public int CategoryNo => Category;

    /// <inheritdoc />
    public int CodeNo => Code;

    /// <inheritdoc />
    public string Name => "メッセージ";
}
