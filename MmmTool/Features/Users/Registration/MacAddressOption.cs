namespace MmmTool.Features.Users.Registration;

/// <summary>
/// 登録の画面で選ぶ、この PC の MAC アドレス (選択肢)。
/// </summary>
/// <param name="Value">MAC アドレス (大文字の 16 進 12 桁・区切りなし)</param>
/// <param name="Name">アダプターの名前 (分からなければ空)</param>
public sealed record MacAddressOption(string Value, string Name)
{
    /// <summary>MAC アドレスを、2 桁ごとに区切った形 (例: <c>9C-6B-00-B5-33-B0</c>)</summary>
    private string Formatted => string.Join('-', Enumerable.Range(0, Value.Length / 2).Select(index => Value.Substring(index * 2, 2)));

    /// <summary>画面に出す形 (名前があれば <c>名前 (MAC アドレス)</c>、無ければ MAC アドレスだけ)</summary>
    public string Display => string.IsNullOrWhiteSpace(Name) ? Formatted : $"{Name} ({Formatted})";
}
