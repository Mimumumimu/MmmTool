namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 通知を出す機能の「通知を受け取る」のオン・オフの登録口。
/// </summary>
/// <remarks>
/// オン・オフは、人ごとに、機能の番号の下の設定として、ユーザーごとの設定 <c>AppUserSetting</c> に 1 件持つ。値は 1 = オン、0 = オフ。
/// 設定の画面は、この登録口に登録された設定を読んで、「通知を受け取る機能」の節を作る。画面は、メッセージやノートの名前を持たない。
/// 通知を出す機能を足すときは、その機能が実装を登録するだけで、画面は変えない。仕様は <c>docs/specs/notification-settings.md</c>。
/// </remarks>
public interface INotificationFeatureSetting
{
    /// <summary>機能番号。<c>CodeMaster</c> と同じ台帳の番号</summary>
    int FeatureNo { get; }

    /// <summary>画面に出す、機能の名前</summary>
    string Name { get; }

    /// <summary>設定の行が無いときの、オン・オフ</summary>
    bool DefaultOn { get; }
}

/// <summary>「通知を受け取る」のオン・オフを、ユーザーごとの設定で読み書きするときの番号と値</summary>
public static class NotificationFeatureSettings
{
    /// <summary>区分番号。機能の番号の下の「通知を受け取る」</summary>
    public const int CategoryNo = 1;

    /// <summary>コード番号。1 つの機能に 1 つのオン・オフは 1</summary>
    public const int CodeNo = 1;

    /// <summary>オンの値</summary>
    public const string On = "1";

    /// <summary>オフの値</summary>
    public const string Off = "0";

    /// <summary>保存された値から、オン・オフを決める</summary>
    /// <param name="saved">保存された値。行が無いときは null</param>
    /// <param name="defaultOn">行が無いときの、オン・オフ</param>
    /// <returns>オンなら true。値が 1 でも 0 でもないときは、行が無いときと同じ</returns>
    public static bool IsOn(string? saved, bool defaultOn) => saved switch
    {
        On => true,
        Off => false,
        _ => defaultOn,
    };

    /// <summary>オン・オフを、保存する値にする</summary>
    /// <param name="isOn">オンなら true</param>
    /// <returns>保存する値</returns>
    public static string ToValue(bool isOn) => isOn ? On : Off;
}
