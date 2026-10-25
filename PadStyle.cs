using System.Globalization;
using System.Windows.Media;

namespace Suzumetry;

public static class PadStyle
{
    public static readonly string[] Palette =
    {
        "#FFFFFF", "#C9CEDA", "#7F8798", "#2A2F3A", "#0B0D11", "#000000",
        "#FF5A7A", "#FF5A5A", "#FF8A1F", "#FFB020", "#FFD21F", "#5CFF8A",
        "#2BD96B", "#4DA6FF", "#3A7BFF", "#B07CFF", "#FF7AD9", "#22D3EE"
    };

    public static bool TryParse(string? s, out Color c)
    {
        c = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().TrimStart('#');
        if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
        if (s.Length != 6) return false;
        if (!int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int v)) return false;
        c = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color Parse(string? s, Color fallback) => TryParse(s, out var c) ? c : fallback;

    public static byte Alpha(double opacity) => (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);

    public static SolidColorBrush MakeBrush(string? hex, double opacity)
    {
        var c = Parse(hex, Colors.White);
        var b = new SolidColorBrush(Color.FromArgb(Alpha(opacity), c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    public static Pen MakePen(string? hex, double opacity, double thickness)
    {
        var p = new Pen(MakeBrush(hex, opacity), Math.Max(0.1, thickness));
        p.Freeze();
        return p;
    }
}
