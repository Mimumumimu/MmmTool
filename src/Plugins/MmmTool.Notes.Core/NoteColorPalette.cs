using System.Globalization;

namespace MmmTool.Notes.Core;

/// <summary>
/// 本文に付けられる文字色のパレット (11 系統 × 4 段階の 44 色)。
/// </summary>
/// <remarks>
/// 色は、系統 (灰色・赤・橙など)と段階 (濃い〜薄い)で決める。本文には、ライトモードで読める色の値 (<see cref="NoteColor.Hex"/>)が入り、
/// 表示のときに、ダークモードでは同じ系統の別の色 (<see cref="NoteColor.DarkHex"/>)に差し替える。パレットに無い値は、色を付けない (標準の文字色になる)。
/// 全部の色は、カードの背景 (ライト <see cref="LightBackground"/>・ダーク <see cref="DarkBackground"/>)に対して、本文の文字として読める明るさ (コントラスト比 4.5 以上)になるよう、計算で決める。
/// 「黒」は置かない (ダークモードで見えなくなるため)。色を外すときは「標準」にする。
/// </remarks>
public static class NoteColorPalette
{
    /// <summary>ライトモードのカードの背景色</summary>
    public const string LightBackground = "#FFFFFF";

    /// <summary>ダークモードのカードの背景色</summary>
    public const string DarkBackground = "#2B2B2B";

    /// <summary>段階の名前 (濃い順)</summary>
    private static readonly string[] ToneNames = ["濃い", "やや濃い", "やや薄い", "薄い"];

    /// <summary>ライトモードで、背景との間に確保するコントラスト比 (段階ごと。濃い順)</summary>
    private static readonly double[] LightContrasts = [9.0, 7.0, 5.5, 4.5];

    /// <summary>ダークモードで、背景との間に確保するコントラスト比 (段階ごと。濃い順)</summary>
    private static readonly double[] DarkContrasts = [4.8, 6.5, 8.5, 11.0];

    /// <summary>系統 (名前・色相・彩度)。灰色だけ彩度 0</summary>
    private static readonly (string Name, double Hue, double Saturation)[] Families =
    [
        ("灰色", 0, 0),
        ("赤", 0, 0.78),
        ("橙", 28, 0.9),
        ("黄", 45, 0.95),
        ("緑", 125, 0.7),
        ("青緑", 170, 0.8),
        ("水色", 190, 0.85),
        ("青", 215, 0.8),
        ("藍", 245, 0.7),
        ("紫", 285, 0.65),
        ("桃", 330, 0.7),
    ];

    /// <summary>パレットの全部の色 (系統の順、系統の中は濃い順)</summary>
    public static IReadOnlyList<NoteColor> All { get; } = Build();

    /// <summary>1 行に並べる色の数 (段階の数。画面のパレットの列の数)</summary>
    public static int ToneCount => ToneNames.Length;

    /// <summary>ライトモードの値から、パレットの色を探す</summary>
    /// <param name="hex">色の値 (<c>#RRGGBB</c>。大文字と小文字は区別しない)</param>
    /// <returns>パレットの色。無ければ null</returns>
    public static NoteColor? TryFind(string hex)
        => All.FirstOrDefault(color => string.Equals(color.Hex, hex, StringComparison.OrdinalIgnoreCase));

    /// <summary>パレットを作る</summary>
    /// <returns>全部の色</returns>
    private static NoteColor[] Build()
    {
        var lightBackground = Luminance(LightBackground);
        var darkBackground = Luminance(DarkBackground);
        var colors = new List<NoteColor>();
        foreach (var (name, hue, saturation) in Families)
        {
            for (var tone = 0; tone < ToneNames.Length; tone++)
            {
                var light = Solve(hue, saturation, lightBackground, LightContrasts[tone], brighterIsBetter: false);
                var dark = Solve(hue, saturation, darkBackground, DarkContrasts[tone], brighterIsBetter: true);
                colors.Add(new NoteColor(colors.Count, $"{name} ({ToneNames[tone]})", light, dark));
            }
        }
        return [.. colors];
    }

    /// <summary>指定のコントラスト比になる明るさの色を、二分法で探す</summary>
    /// <param name="hue">色相 (0〜360)</param>
    /// <param name="saturation">彩度 (0〜1)</param>
    /// <param name="backgroundLuminance">背景の相対輝度</param>
    /// <param name="contrast">確保するコントラスト比</param>
    /// <param name="brighterIsBetter">明るいほどコントラストが上がるか (ダークモードの背景は true、ライトモードは false)</param>
    /// <returns>色 (<c>#RRGGBB</c>)</returns>
    private static string Solve(double hue, double saturation, double backgroundLuminance, double contrast, bool brighterIsBetter)
    {
        double low = 0, high = 1;
        for (var i = 0; i < 40; i++)
        {
            var middle = (low + high) / 2;
            var ratio = Contrast(Luminance(FromHsl(hue, saturation, middle)), backgroundLuminance);
            // 目標より足りないとき、明るさを動かす向きは、背景が暗いなら明るく、背景が明るいなら暗く
            var enough = ratio >= contrast;
            if (brighterIsBetter)
            {
                if (enough) { high = middle; } else { low = middle; }
            }
            else
            {
                if (enough) { low = middle; } else { high = middle; }
            }
        }
        // 確実に目標以上になる側の端を採る (丸めで、わずかに下回らないように)
        var lightness = brighterIsBetter ? high : low;
        return ToHex(FromHsl(hue, saturation, lightness));
    }

    /// <summary>HSL から RGB にする</summary>
    /// <param name="hue">色相 (0〜360)</param>
    /// <param name="saturation">彩度 (0〜1)</param>
    /// <param name="lightness">明度 (0〜1)</param>
    /// <returns>赤・緑・青 (0〜1)</returns>
    private static (double R, double G, double B) FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var h = hue / 60;
        var x = chroma * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)Math.Floor(h) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        var m = lightness - chroma / 2;
        return (r + m, g + m, b + m);
    }

    /// <summary>RGB を <c>#RRGGBB</c> にする</summary>
    /// <param name="rgb">赤・緑・青 (0〜1)</param>
    /// <returns>大文字の <c>#RRGGBB</c></returns>
    private static string ToHex((double R, double G, double B) rgb)
        => string.Create(CultureInfo.InvariantCulture, $"#{Byte(rgb.R):X2}{Byte(rgb.G):X2}{Byte(rgb.B):X2}");

    /// <summary>0〜1 を 0〜255 に丸める</summary>
    /// <param name="value">0〜1 の値</param>
    /// <returns>0〜255 の整数</returns>
    private static int Byte(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255);

    /// <summary>相対輝度 (WCAG)を求める</summary>
    /// <param name="hex"><c>#RRGGBB</c></param>
    /// <returns>相対輝度</returns>
    private static double Luminance(string hex)
    {
        var value = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Luminance(((value >> 16 & 0xFF) / 255.0, (value >> 8 & 0xFF) / 255.0, (value & 0xFF) / 255.0));
    }

    /// <summary>相対輝度 (WCAG)を求める</summary>
    /// <param name="rgb">赤・緑・青 (0〜1)</param>
    /// <returns>相対輝度</returns>
    /// <remarks>丸めた 8 ビットの値で求める (表示される色のコントラスト比と合わせるため)。</remarks>
    private static double Luminance((double R, double G, double B) rgb)
    {
        static double Linear(double channel)
        {
            var c = Math.Round(Math.Clamp(channel, 0, 1) * 255) / 255;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(rgb.R) + 0.7152 * Linear(rgb.G) + 0.0722 * Linear(rgb.B);
    }

    /// <summary>コントラスト比を求める</summary>
    /// <param name="first">片方の相対輝度</param>
    /// <param name="second">もう片方の相対輝度</param>
    /// <returns>コントラスト比 (1〜21)</returns>
    private static double Contrast(double first, double second)
    {
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }
}
