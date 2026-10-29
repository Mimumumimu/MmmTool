using MmmTool.CodeMaster.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Messages.Core;

/// <summary>
/// メッセージの「通知を受け取る」のオン・オフ。ユーザーごとの設定 <c>AppUserSetting</c> の 1 行で、行が無いときはオン。
/// </summary>
/// <remarks>
/// 受け取り先を指定すれば、そのまま通知を受け取れるよう、既定はオン。設定の画面は、登録されたこの設定を読んで、「通知を受け取る機能」の節を作る。
/// </remarks>
public sealed class MessageNotificationSetting : INotificationFeatureSetting
{
    /// <inheritdoc />
    public int FeatureNo => CodeFeatures.Messages;

    /// <inheritdoc />
    public string Name => "メッセージ";

    /// <inheritdoc />
    public bool DefaultOn => true;
}
