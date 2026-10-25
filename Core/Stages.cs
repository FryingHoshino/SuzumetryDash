using System.Globalization;
using System.Text.Json;

namespace Suzumetry.Core;

public static class Stages
{
    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Suzumetry");
    static string FilePath => Path.Combine(Dir, "stages.json");

    public static string Name(int lengthM)
    {
        if (lengthM <= 0) return "";
        var fallback = (lengthM / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " km stage";
        try
        {
            var map = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, string>()
                : new Dictionary<string, string>();
            var key = lengthM.ToString(CultureInfo.InvariantCulture);
            if (map.TryGetValue(key, out var n))
            {
                if (!string.IsNullOrWhiteSpace(n)) return n.Trim();
            }
            else
            {
                map[key] = "";
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception e)
        {
            Log.Write(e);
        }
        return fallback;
    }
}
