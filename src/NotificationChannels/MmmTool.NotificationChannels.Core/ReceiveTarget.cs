using MmmTool.CodeMaster.Core;

namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 通知の受け取り先の保存先を決める番号。ユーザーごとの設定 <c>AppUserSetting</c> の 1 行で、値が送信先の番号。
/// </summary>
/// <remarks>
/// 受け取り先は、どの機能にも属さない共通の設定で、人ごとに 1 つ。機能番号は共通の <see cref="CodeFeatures.Common"/>。
/// 仕様は <c>docs/specs/notification-settings.md</c>。
/// </remarks>
public static class ReceiveTarget
{
    /// <summary>機能番号。共通</summary>
    public const int FeatureNo = CodeFeatures.Common;

    /// <summary>区分番号。受け取り先</summary>
    public const int CategoryNo = 1;

    /// <summary>コード番号。1 人に 1 つの受け取り先は 1</summary>
    public const int CodeNo = 1;
}
