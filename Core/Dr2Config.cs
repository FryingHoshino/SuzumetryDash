using System.Xml.Linq;

namespace Suzumetry.Core;

public static class Dr2Config
{
    public static string Patch(int port)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "DiRT Rally 2.0", "hardwaresettings", "hardware_settings_config.xml");
            if (!File.Exists(path)) return "Config not found:\n" + path;
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var udp = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "udp");
            if (udp == null) return "udp node not found in config.";
            var bak = path + ".suzumetry.bak";
            if (!File.Exists(bak)) File.Copy(path, bak);
            udp.SetAttributeValue("enabled", "true");
            udp.SetAttributeValue("extradata", "3");
            udp.SetAttributeValue("ip", "127.0.0.1");
            udp.SetAttributeValue("port", port.ToString());
            udp.SetAttributeValue("delay", "1");
            doc.Save(path, SaveOptions.DisableFormatting);
            return "DR2 UDP telemetry enabled. Restart the game.";
        }
        catch (Exception e)
        {
            Log.Write(e);
            return "Patch failed: " + e.Message;
        }
    }
}
