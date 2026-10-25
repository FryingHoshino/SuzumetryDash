using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Suzumetry.Core;

public sealed class AxisInput
{
    public static readonly string[] XInputAxes = { "Left stick X", "Left stick Y", "Right stick X", "Right stick Y", "Left trigger", "Right trigger", "None" };
    public static readonly string[] JoyAxes = { "X", "Y", "Z", "R (rudder)", "U", "V", "None" };

    static readonly ConcurrentDictionary<int, (DeviceDesc Desc, long Stamp)> cache = new();
    static readonly ConcurrentDictionary<int, byte> pending = new();
    static volatile bool xBroken;

    [StructLayout(LayoutKind.Sequential)]
    struct XState
    {
        public uint Packet;
        public ushort Buttons;
        public byte Lt;
        public byte Rt;
        public short Lx;
        public short Ly;
        public short Rx;
        public short Ry;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct XCaps
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public ushort GButtons;
        public byte Lt;
        public byte Rt;
        public short Lx;
        public short Ly;
        public short Rx;
        public short Ry;
        public ushort VibL;
        public ushort VibR;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JoyInfoEx
    {
        public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Res1, Res2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct JoyCaps
    {
        public ushort Mid;
        public ushort Pid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        public uint Xmin, Xmax, Ymin, Ymax, Zmin, Zmax, NumButtons, PeriodMin, PeriodMax, Rmin, Rmax, Umin, Umax, Vmin, Vmax, Caps, MaxAxes, NumAxes, MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Oem;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern uint XGetState(uint index, out XState state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
    static extern uint XGetCaps(uint index, uint flags, out XCaps caps);

    [DllImport("winmm.dll")]
    static extern uint joyGetPosEx(uint id, ref JoyInfoEx info);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    static extern uint joyGetDevCapsW(UIntPtr id, out JoyCaps caps, uint size);

    readonly uint[] min = new uint[6];
    readonly uint[] max = new uint[6];
    int capId = -1;

    public void Reset() => capId = -1;

    static uint XS(int index, out XState s)
    {
        s = default;
        if (xBroken) return 1;
        try
        {
            return XGetState((uint)index, out s);
        }
        catch (Exception)
        {
            xBroken = true;
            return 1;
        }
    }

    static uint XC(int index, out XCaps c)
    {
        c = default;
        if (xBroken) return 1;
        try
        {
            return XGetCaps((uint)index, 0, out c);
        }
        catch (Exception)
        {
            xBroken = true;
            return 1;
        }
    }

    static double Th(short v) => Math.Clamp(v / 32767.0, -1, 1);

    static double XAxis(in XState s, int a) => a switch
    {
        0 => Th(s.Lx),
        1 => Th(s.Ly),
        2 => Th(s.Rx),
        3 => Th(s.Ry),
        4 => s.Lt / 255.0 * 2 - 1,
        5 => s.Rt / 255.0 * 2 - 1,
        _ => 0
    };

    void LoadCaps(int id)
    {
        capId = id;
        for (int i = 0; i < 6; i++)
        {
            min[i] = 0;
            max[i] = 65535;
        }
        if (joyGetDevCapsW((UIntPtr)(uint)id, out var c, (uint)Marshal.SizeOf<JoyCaps>()) != 0) return;
        min[0] = c.Xmin; max[0] = c.Xmax;
        min[1] = c.Ymin; max[1] = c.Ymax;
        min[2] = c.Zmin; max[2] = c.Zmax;
        min[3] = c.Rmin; max[3] = c.Rmax;
        min[4] = c.Umin; max[4] = c.Umax;
        min[5] = c.Vmin; max[5] = c.Vmax;
    }

    double JAxis(in JoyInfoEx i, int a)
    {
        if (a < 0 || a > 5) return 0;
        uint raw = a switch { 0 => i.X, 1 => i.Y, 2 => i.Z, 3 => i.R, 4 => i.U, _ => i.V };
        double lo = min[a], hi = max[a];
        double v = hi > lo ? (raw - lo) / (hi - lo) * 2 - 1 : (raw - 32767.5) / 32767.5;
        v = Math.Clamp(v, -1, 1);
        return a == 1 ? -v : v;
    }

    public bool Poll(int source, int index, int axisX, int axisY, out double x, out double y)
    {
        x = 0;
        y = 0;
        try
        {
            if (source == 1)
            {
                if (XS(index, out var s) != 0) return false;
                x = XAxis(s, axisX);
                y = XAxis(s, axisY);
                return true;
            }
            if (capId != index) LoadCaps(index);
            var info = new JoyInfoEx { Size = (uint)Marshal.SizeOf<JoyInfoEx>(), Flags = 0x3F };
            if (joyGetPosEx((uint)index, ref info) != 0) return false;
            x = JAxis(info, axisX);
            y = JAxis(info, axisY);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static string XKind(int i) => XC(i, out var c) == 0 && c.SubType == 2 ? "Wheel" : "Gamepad";

    static string? XName(int i)
    {
        if (XS(i, out _) != 0) return null;
        return XKind(i) == "Wheel" ? "Wheel (XInput)" : "Gamepad (XInput)";
    }

    static string? JoyName(int id)
    {
        var info = new JoyInfoEx { Size = (uint)Marshal.SizeOf<JoyInfoEx>(), Flags = 0x3F };
        if (joyGetPosEx((uint)id, ref info) != 0) return null;
        if (joyGetDevCapsW((UIntPtr)(uint)id, out var caps, (uint)Marshal.SizeOf<JoyCaps>()) != 0) return "Joystick";
        string name = caps.Name ?? "Joystick";
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\" + caps.RegKey);
            if (k?.GetValue("OEMName") is string s && s.Length > 0) name = s;
        }
        catch (Exception)
        {
        }
        return name;
    }

    public static string[] ListDevices(int source)
    {
        int n = source == 1 ? 4 : 16;
        var res = new string[n];
        for (int i = 0; i < n; i++)
        {
            string? name = null;
            try { name = source == 1 ? XName(i) : JoyName(i); } catch (Exception) { }
            res[i] = (source == 1 ? "Controller " : "Joystick ") + (i + 1) + " - " + (name ?? "not connected");
        }
        return res;
    }

    public static DeviceDesc? Describe(int source, int index)
    {
        int key = source * 100 + index;
        long now = Environment.TickCount64;
        if (cache.TryGetValue(key, out var e))
        {
            if (now - e.Stamp > 10000 && pending.TryAdd(key, 0)) Refresh(key, source, index);
            return e.Desc;
        }
        if (pending.TryAdd(key, 0)) Refresh(key, source, index);
        return null;
    }

    static void Refresh(int key, int source, int index)
    {
        Task.Run(() =>
        {
            try { cache[key] = (BuildDesc(source, index), Environment.TickCount64); }
            catch (Exception ex) { Log.Write(ex); }
            finally { pending.TryRemove(key, out _); }
        });
    }

    static DeviceDesc BuildDesc(int source, int index)
    {
        var d = new DeviceDesc { Handle = source * 100 + index };
        if (source == 1)
        {
            d.Kind = XKind(index);
            d.Name = d.Kind == "Wheel" ? "XInput wheel" : "XInput controller";
            d.Extra = "XInput slot " + (index + 1);
            return d;
        }
        d.Kind = "Wheel / Joystick";
        d.Name = JoyName(index) ?? "Joystick " + (index + 1);
        d.Extra = "Joystick " + (index + 1);
        if (joyGetDevCapsW((UIntPtr)(uint)index, out var c, (uint)Marshal.SizeOf<JoyCaps>()) == 0)
        {
            d.Vid = c.Mid;
            d.Pid = c.Pid;
            if (d.Vid != 0 || d.Pid != 0)
            {
                foreach (var r in InputDevices.ListAll())
                {
                    if (r.Vid == d.Vid && r.Pid == d.Pid && r.Kind != "Mouse")
                    {
                        d.Bus = r.Bus;
                        d.Port = r.Port;
                        break;
                    }
                }
            }
        }
        return d;
    }
}
