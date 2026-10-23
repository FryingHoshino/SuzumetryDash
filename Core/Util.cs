using System.Text;

namespace Suzumetry.Core;

public static class Log
{
    static readonly object gate = new();

    public static void Write(Exception e)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Suzumetry");
            Directory.CreateDirectory(dir);
            var p = Path.Combine(dir, "log.txt");
            lock (gate)
            {
                if (File.Exists(p) && new FileInfo(p).Length > 512 * 1024) File.Delete(p);
                File.AppendAllText(p, DateTime.Now.ToString("s") + " " + e + Environment.NewLine);
            }
        }
        catch { }
    }
}

public static class Bin
{
    public static float F(byte[] b, int o)
    {
        if (o < 0 || o + 4 > b.Length) return 0f;
        var v = BitConverter.ToSingle(b, o);
        return float.IsFinite(v) ? v : 0f;
    }

    public static int I(byte[] b, int o) => o < 0 || o + 4 > b.Length ? 0 : BitConverter.ToInt32(b, o);

    public static string S(byte[] b, int o, int chars)
    {
        int max = Math.Min(chars * 2, b.Length - o);
        if (max <= 1) return "";
        int n = 0;
        while (n + 1 < max && (b[o + n] != 0 || b[o + n + 1] != 0)) n += 2;
        return Encoding.Unicode.GetString(b, o, n).Trim();
    }
}

public static class Fmt
{
    public static string Time(int ms)
    {
        if (ms <= 0 || ms >= 3600000) return "--:--.---";
        return $"{ms / 60000}:{ms / 1000 % 60:00}.{ms % 1000:000}";
    }
}
