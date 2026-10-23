using System.Text.Json;

namespace Suzumetry.Core;

public sealed class Settings
{
    public int ReadHz { get; set; } = 120;
    public int StreamHz { get; set; } = 60;
    public int Port { get; set; } = 8080;
    public int Dr2Port { get; set; } = 20777;
    public int ForceGame { get; set; } = 0;
    public bool HudEnabled { get; set; } = true;
    public double HudX { get; set; } = 40;
    public double HudY { get; set; } = 40;
    public double HudScale { get; set; } = 1;
    public double HudOpacity { get; set; } = 0.92;
    public bool HudPedals { get; set; } = true;
    public bool HudTyres { get; set; } = true;
    public bool HudLaps { get; set; } = true;
    public bool HudFuel { get; set; } = true;

    static readonly object gate = new();
    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Suzumetry");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                s.Clamp();
                return s;
            }
        }
        catch (Exception e) { Log.Write(e); }
        return new Settings();
    }

    public void Clamp()
    {
        ReadHz = Math.Clamp(ReadHz, 10, 500);
        StreamHz = Math.Clamp(StreamHz, 1, 240);
        Port = Math.Clamp(Port, 1024, 65535);
        Dr2Port = Math.Clamp(Dr2Port, 1024, 65535);
        ForceGame = Math.Clamp(ForceGame, 0, 3);
        HudScale = Math.Clamp(HudScale, 0.4, 3);
        HudOpacity = Math.Clamp(HudOpacity, 0.2, 1);
    }

    public void Save()
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Dir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, true);
            }
        }
        catch (Exception e) { Log.Write(e); }
    }
}
