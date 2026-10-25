using System.Reflection;
using System.Text.Json;

namespace Suzumetry.Core;

public static class Presets
{
    sealed class PresetFile
    {
        public string Name { get; set; } = "";
        public Dictionary<string, JsonElement> Values { get; set; } = new();
    }

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Suzumetry", "Presets");

    static bool Included(PropertyInfo p) =>
        p.CanRead && p.CanWrite
        && (p.PropertyType == typeof(string) || p.PropertyType == typeof(double) || p.PropertyType == typeof(int) || p.PropertyType == typeof(bool))
        && (p.Name.StartsWith("Hs") || p.Name.StartsWith("Pad") || p.Name == "HudExtra" || p.Name == "HudShift" || p.Name == "HudWarn");

    public static string Clean(string? name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var s = new string((name ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return s.Length > 60 ? s[..60] : s;
    }

    static string FileOf(string name) => Path.Combine(Dir, Clean(name) + ".json");

    public static List<string> List()
    {
        try
        {
            if (!Directory.Exists(Dir)) return new List<string>();
            return Directory.GetFiles(Dir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception e)
        {
            Log.Write(e);
            return new List<string>();
        }
    }

    public static bool Save(Settings st, string name)
    {
        try
        {
            name = Clean(name);
            if (name.Length == 0) return false;
            Directory.CreateDirectory(Dir);
            var f = new PresetFile { Name = name };
            foreach (var p in typeof(Settings).GetProperties().Where(Included))
                f.Values[p.Name] = JsonSerializer.SerializeToElement(p.GetValue(st), p.PropertyType);
            File.WriteAllText(FileOf(name), JsonSerializer.Serialize(f, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception e)
        {
            Log.Write(e);
            return false;
        }
    }

    public static bool Load(Settings st, string name)
    {
        try
        {
            var path = FileOf(name);
            if (!File.Exists(path)) return false;
            return Apply(st, File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Log.Write(e);
            return false;
        }
    }

    static bool Apply(Settings st, string json)
    {
        var f = JsonSerializer.Deserialize<PresetFile>(json);
        if (f == null || f.Values.Count == 0) return false;
        foreach (var p in typeof(Settings).GetProperties().Where(Included))
        {
            if (!f.Values.TryGetValue(p.Name, out var v)) continue;
            try
            {
                if (p.PropertyType == typeof(string) && v.ValueKind == JsonValueKind.String) p.SetValue(st, v.GetString());
                else if (p.PropertyType == typeof(bool) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)) p.SetValue(st, v.GetBoolean());
                else if (p.PropertyType == typeof(double) && v.ValueKind == JsonValueKind.Number) p.SetValue(st, v.GetDouble());
                else if (p.PropertyType == typeof(int) && v.ValueKind == JsonValueKind.Number) p.SetValue(st, (int)Math.Round(v.GetDouble()));
            }
            catch (Exception)
            {
            }
        }
        st.Clamp();
        return true;
    }

    public static bool Delete(string name)
    {
        try
        {
            var path = FileOf(name);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception e)
        {
            Log.Write(e);
            return false;
        }
    }

    public static bool Export(string name, string target)
    {
        try
        {
            var path = FileOf(name);
            if (!File.Exists(path)) return false;
            File.Copy(path, target, true);
            return true;
        }
        catch (Exception e)
        {
            Log.Write(e);
            return false;
        }
    }

    public static string? Import(string source)
    {
        try
        {
            var text = File.ReadAllText(source);
            var f = JsonSerializer.Deserialize<PresetFile>(text);
            if (f == null || f.Values.Count == 0) return null;
            var baseName = Clean(Path.GetFileNameWithoutExtension(source));
            if (baseName.Length == 0) baseName = "Imported";
            var name = baseName;
            int n = 2;
            while (File.Exists(FileOf(name))) name = baseName + " (" + n++ + ")";
            Directory.CreateDirectory(Dir);
            f.Name = name;
            File.WriteAllText(FileOf(name), JsonSerializer.Serialize(f, new JsonSerializerOptions { WriteIndented = true }));
            return name;
        }
        catch (Exception e)
        {
            Log.Write(e);
            return null;
        }
    }
}
