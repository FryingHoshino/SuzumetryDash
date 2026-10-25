using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Suzumetry.Core;

public sealed class DeviceDesc
{
    public long Handle { get; init; }
    public string Kind { get; set; } = "Device";
    public string Name { get; set; } = "";
    public int Vid { get; set; }
    public int Pid { get; set; }
    public string Bus { get; set; } = "";
    public string Port { get; set; } = "";
    public string Path { get; set; } = "";
    public string Extra { get; set; } = "";
    public string Id => Vid != 0 || Pid != 0 ? $"VID {Vid:X4} PID {Pid:X4}" : "";
}

public static class InputDevices
{
    static readonly ConcurrentDictionary<long, DeviceDesc> cache = new();
    static readonly ConcurrentDictionary<long, byte> pending = new();
    static readonly Regex VidPid = new(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex PortLoc = new(@"Port_#(\d+)\.Hub_#(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [StructLayout(LayoutKind.Sequential)]
    struct RawDevice
    {
        public IntPtr Handle;
        public uint Type;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetRawInputDeviceList([Out] RawDevice[]? list, ref uint count, uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    static extern uint GetInfo(IntPtr device, uint command, IntPtr data, ref uint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("hid.dll")]
    static extern bool HidD_GetProductString(IntPtr handle, byte[] buffer, uint length);

    [DllImport("hid.dll")]
    static extern bool HidD_GetManufacturerString(IntPtr handle, byte[] buffer, uint length);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern uint CM_Locate_DevNodeW(out uint dev, string id, uint flags);

    [DllImport("cfgmgr32.dll")]
    static extern uint CM_Get_Parent(out uint parent, uint dev, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern uint CM_Get_Device_IDW(uint dev, StringBuilder buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern uint CM_Get_DevNode_Registry_PropertyW(uint dev, uint property, out uint type, byte[] buffer, ref uint length, uint flags);

    public static DeviceDesc? Get(long handle)
    {
        if (handle == 0) return null;
        if (cache.TryGetValue(handle, out var d)) return d;
        if (pending.TryAdd(handle, 0))
        {
            Task.Run(() =>
            {
                try { cache[handle] = Query(handle); }
                catch (Exception e) { Log.Write(e); }
                finally { pending.TryRemove(handle, out _); }
            });
        }
        return null;
    }

    public static void Clear() => cache.Clear();

    public static List<DeviceDesc> ListAll()
    {
        var res = new List<DeviceDesc>();
        try
        {
            uint count = 0;
            uint size = (uint)Marshal.SizeOf<RawDevice>();
            GetRawInputDeviceList(null, ref count, size);
            if (count == 0) return res;
            var list = new RawDevice[count];
            uint got = GetRawInputDeviceList(list, ref count, size);
            if (got == uint.MaxValue) return res;
            for (int i = 0; i < got; i++)
            {
                var h = list[i].Handle;
                if (list[i].Type == 1) continue;
                var d = Query(h.ToInt64());
                if (d.Kind == "Mouse" || d.Kind == "Gamepad" || d.Kind == "Wheel / Joystick" || d.Kind == "Controller")
                {
                    cache[d.Handle] = d;
                    res.Add(d);
                }
            }
        }
        catch (Exception e)
        {
            Log.Write(e);
        }
        return res;
    }

    static string Detail(DeviceDesc d, int level) => level switch
    {
        0 => "",
        1 => string.Join(" · ", new[] { d.Id, d.Extra }.Where(x => x.Length > 0)),
        _ => string.Join(" · ", new[] { d.Id, d.Extra, d.Bus, d.Port }.Where(x => x.Length > 0))
    };

    public static void Caption(PadState ps, Settings st, out string head, out string detail)
    {
        string word = ps.Mode == 1 ? "GAMEPAD" : ps.Mode == 2 ? "WHEEL" : "MOUSE";
        if (!st.PadCapInfo)
        {
            head = word;
            detail = "";
            return;
        }
        if (ps.Mode != 0)
        {
            if (ps.Missing)
            {
                head = word + " · not connected";
                detail = "Pick the device in the pad settings";
                return;
            }
            var s = ps.Source;
            if (s == null)
            {
                head = word;
                detail = "Reading device...";
                return;
            }
            head = s.Kind.ToUpperInvariant() + " · " + s.Name;
            detail = Detail(s, st.PadCapDetail);
            return;
        }
        var d = Get(ps.Device);
        if (d == null)
        {
            head = word;
            detail = ps.Device == 0 ? "Move the mouse to detect the device" : "Reading device...";
            return;
        }
        head = d.Kind.ToUpperInvariant() + " · " + d.Name;
        detail = Detail(d, st.PadCapDetail);
    }

    static DeviceDesc Query(long handle)
    {
        var h = new IntPtr(handle);
        var d = new DeviceDesc { Handle = handle };
        string path = NameOf(h);
        d.Path = path;
        uint type = 99;
        int usagePage = 0, usage = 0;
        InfoOf(h, out type, out usagePage, out usage);
        d.Kind = type switch
        {
            0 => "Mouse",
            1 => "Keyboard",
            2 => usagePage == 1 && usage == 5 ? "Gamepad"
                : usagePage == 1 && usage == 4 ? "Wheel / Joystick"
                : usagePage == 1 && usage == 8 ? "Controller"
                : "HID",
            _ => "Device"
        };
        var m = VidPid.Match(path);
        if (m.Success)
        {
            d.Vid = Convert.ToInt32(m.Groups[1].Value, 16);
            d.Pid = Convert.ToInt32(m.Groups[2].Value, 16);
        }
        string up = path.ToUpperInvariant();
        if (up.Contains("BTH") || up.Contains("{00001124")) d.Bus = "Bluetooth";
        else if (up.Contains("ROOT#") || up.Contains("RDP")) d.Bus = "Virtual";
        else if (up.Contains("ACPI") || up.Contains("PNP0F") || up.Contains("I8042")) d.Bus = "PS/2 or internal";
        else if (m.Success) d.Bus = "USB";
        string product = "", maker = "";
        if (path.Length > 0)
        {
            var fh = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (fh != new IntPtr(-1) && fh != IntPtr.Zero)
            {
                try
                {
                    product = HidString(fh, true);
                    maker = HidString(fh, false);
                }
                finally
                {
                    CloseHandle(fh);
                }
            }
            Locate(d, path);
        }
        if (product.Length > 0) d.Name = product;
        else if (maker.Length > 0) d.Name = maker + " " + d.Kind.ToLowerInvariant();
        else d.Name = d.Kind + (d.Id.Length > 0 ? " (" + d.Id + ")" : "");
        return d;
    }

    static string HidString(IntPtr h, bool product)
    {
        var buf = new byte[256];
        bool ok = product ? HidD_GetProductString(h, buf, (uint)buf.Length) : HidD_GetManufacturerString(h, buf, (uint)buf.Length);
        if (!ok) return "";
        var s = Encoding.Unicode.GetString(buf);
        int z = s.IndexOf('\0');
        return (z >= 0 ? s[..z] : s).Trim();
    }

    static string NameOf(IntPtr h)
    {
        uint n = 0;
        GetInfo(h, 0x20000007, IntPtr.Zero, ref n);
        if (n == 0) return "";
        IntPtr p = Marshal.AllocHGlobal((int)n * 2 + 2);
        try
        {
            uint sz = n;
            uint r = GetInfo(h, 0x20000007, p, ref sz);
            if (r == uint.MaxValue || r == 0) return "";
            return Marshal.PtrToStringUni(p) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(p);
        }
    }

    static bool InfoOf(IntPtr h, out uint type, out int usagePage, out int usage)
    {
        type = 99;
        usagePage = 0;
        usage = 0;
        IntPtr p = Marshal.AllocHGlobal(32);
        try
        {
            for (int i = 0; i < 32; i += 8) Marshal.WriteInt64(p, i, 0);
            Marshal.WriteInt32(p, 0, 32);
            uint sz = 32;
            uint r = GetInfo(h, 0x2000000b, p, ref sz);
            if (r == uint.MaxValue) return false;
            type = (uint)Marshal.ReadInt32(p, 4);
            usagePage = (ushort)Marshal.ReadInt16(p, 20);
            usage = (ushort)Marshal.ReadInt16(p, 22);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(p);
        }
    }

    static void Locate(DeviceDesc d, string path)
    {
        string id = path;
        if (id.StartsWith(@"\\?\")) id = id[4..];
        int k = id.LastIndexOf("#{", StringComparison.Ordinal);
        if (k > 0) id = id[..k];
        id = id.Replace('#', '\\');
        if (CM_Locate_DevNodeW(out uint dev, id, 0) != 0) return;
        for (int i = 0; i < 8; i++)
        {
            var sb = new StringBuilder(260);
            if (CM_Get_Device_IDW(dev, sb, 260, 0) == 0)
            {
                string s = sb.ToString();
                if (s.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)
                    && s.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) < 0
                    && s.IndexOf("ROOT_HUB", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    d.Bus = "USB";
                    string loc = Prop(dev, 0x0D);
                    var m = PortLoc.Match(loc);
                    d.Port = m.Success ? "Port " + int.Parse(m.Groups[1].Value) + " / Hub " + int.Parse(m.Groups[2].Value) : loc;
                    return;
                }
            }
            if (CM_Get_Parent(out uint par, dev, 0) != 0) break;
            dev = par;
        }
    }

    static string Prop(uint dev, uint prop)
    {
        var buf = new byte[512];
        uint len = (uint)buf.Length;
        if (CM_Get_DevNode_Registry_PropertyW(dev, prop, out _, buf, ref len, 0) != 0) return "";
        return Encoding.Unicode.GetString(buf, 0, (int)Math.Min(len, (uint)buf.Length)).TrimEnd('\0');
    }
}
