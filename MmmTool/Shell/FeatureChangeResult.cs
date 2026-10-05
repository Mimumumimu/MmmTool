namespace MmmTool.Shell;

/// <summary>機能のオン・オフを切り替えた結果</summary>
public enum FeatureChangeResult
{
    /// <summary>切り替えた</summary>
    Changed,
    /// <summary>オフにする前の確認で取りやめた</summary>
    Cancelled,
    /// <summary>設定を保存できなかったため、切り替えなかった</summary>
    NotSaved,
}
