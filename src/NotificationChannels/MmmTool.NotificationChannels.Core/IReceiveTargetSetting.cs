namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 送信先を、受け取り先として指定できる設定の登録口。
/// </summary>
/// <remarks>
/// 受け取り先は、人ごとに、機能ごとの設定として、ユーザーごとの設定 <c>AppUserSetting</c> に 1 件持つ。値は送信先の番号。
/// 送信先の設定画面は、この登録口に登録された設定を読んで、「受け取り先」の節を作る。画面は、メッセージなどの機能の名前を持たない。
/// 受け取り先を指定する機能を足すときは、その機能が実装を登録するだけで、画面は変えない。
/// </remarks>
public interface IReceiveTargetSetting
{
    /// <summary>機能番号。<c>CodeMaster</c> と同じ台帳の番号</summary>
    int FeatureNo { get; }

    /// <summary>区分番号。機能の中の、受け取り先の種類</summary>
    int CategoryNo { get; }

    /// <summary>コード番号で、枝番。1 人に 1 つの受け取り先は 1</summary>
    int CodeNo { get; }

    /// <summary>画面に出す、受け取り先の名前</summary>
    string Name { get; }
}
