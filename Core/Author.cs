using System.Windows;
using System.Windows.Media.Imaging;

namespace Suzumetry.Core;

public static class Author
{
    public const string Name = "FryingHoshino";
    public static readonly byte[]? Image = Load("author");
    public static readonly byte[]? Chibi = Load("chibi");
    public static string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "F";
    public static string Mime => MimeOf(Image);
    public static BitmapImage? ToBitmapImage(byte[]? data)
    {
        if (data == null || data.Length == 0) return null;

        using var ms = new MemoryStream(data);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze(); 
        return bitmap;
    }
    public static string MimeOf(byte[]? d)
    {
        if (d == null || d.Length < 12) return "image/png";
        if (d[0] == 0xFF && d[1] == 0xD8) return "image/jpeg";
        if (d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46) return "image/gif";
        if (d[0] == 0x52 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x46 && d[8] == 0x57) return "image/webp";
        return "image/png";
    }
    private static byte[]? Load(string baseName)
    {
        foreach (var ext in new[] { ".png", ".gif", ".webp", ".jpg", ".jpeg" })
        {
            var n = baseName + ext;
            var d = FromManifest(n) ?? FromPack(n);
            if (d != null && d.Length > 0) return d;
        }
        return null;
    }
    private static byte[]? FromManifest(string n)
    {
        try
        {
            return Read(typeof(Author).Assembly.GetManifestResourceStream(n));
        }
        catch
        {
            return null;
        }
    }
    private static byte[]? FromPack(string n)
    {
        try
        {
            var r = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/" + n, UriKind.Absolute));
            return r == null ? null : Read(r.Stream);
        }
        catch
        {
            return null;
        }
    }
    private static byte[]? Read(Stream? s)
    {
        if (s == null) return null;
        using (s)
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }
}