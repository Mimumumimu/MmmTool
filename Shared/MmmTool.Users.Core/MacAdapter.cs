namespace MmmTool.Users.Core;

/// <summary>
/// この PC のネットワークアダプター 1 つ (登録の画面で選ぶ MAC アドレスと、その名前)。
/// </summary>
/// <param name="MacAddress">MAC アドレス (大文字の 16 進 12 桁・区切りなし)</param>
/// <param name="Name">アダプターの名前 (Windows が出す説明。分からなければ空)</param>
public sealed record MacAdapter(string MacAddress, string Name);
