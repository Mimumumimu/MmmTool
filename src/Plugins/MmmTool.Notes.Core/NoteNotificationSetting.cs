using MmmTool.CodeMaster.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Notes.Core;

/// <summary>
/// ノートの「通知を受け取る」のオン・オフ。ユーザーごとの設定 <c>AppUserSetting</c> の 1 行で、行が無いときはオフ。
/// </summary>
/// <remarks>
/// 受け取り先を持つ人に、ノートの通知が黙って届き始めないよう、既定はオフ。設定の画面は、登録されたこの設定を読んで、「通知を受け取る機能」の節を作る。
/// </remarks>
public sealed class NoteNotificationSetting : INotificationFeatureSetting
{
    /// <inheritdoc />
    public int FeatureNo => CodeFeatures.Notes;

    /// <inheritdoc />
    public string Name => "ノート";

    /// <inheritdoc />
    public bool DefaultOn => false;
}
