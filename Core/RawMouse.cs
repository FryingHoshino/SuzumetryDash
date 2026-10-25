using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Suzumetry.Core;

public sealed class RawMouse
{
    long dx, dy, events, wheel, active;
    volatile int buttons;
    Thread? thread;
    Dispatcher? disp;
    HwndSource? src;
    IntPtr mem;
    readonly byte[] buf = new byte[256];
    readonly ManualResetEventSlim ready = new(false);

    public long Events => Interlocked.Read(ref events);
    public long Wheel => Interlocked.Read(ref wheel);
    public int Buttons => buttons;
    public long Active => Interlocked.Read(ref active);

    public void Take(out long x, out long y)
    {
        x = Interlocked.Exchange(ref dx, 0);
        y = Interlocked.Exchange(ref dy, 0);
    }

    public void Start()
    {
        if (thread != null) return;
        ready.Reset();
        thread = new Thread(Run) { IsBackground = true, Name = "Suzumetry.RawMouse" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait(2000);
    }

    public void Stop()
    {
        try { disp?.BeginInvokeShutdown(DispatcherPriority.Normal); } catch { }
        thread?.Join(2000);
        thread = null;
        disp = null;
    }

    void Run()
    {
        mem = Marshal.AllocHGlobal(buf.Length);
        try
        {
            disp = Dispatcher.CurrentDispatcher;
            src = new HwndSource(new HwndSourceParameters("SuzumetryRawMouse")
            {
                ParentWindow = new IntPtr(-3),
                WindowStyle = 0,
                Width = 0,
                Height = 0
            });
            src.AddHook(Hook);
            var d = new Native.RAWINPUTDEVICE[] { new() { usUsagePage = 1, usUsage = 2, dwFlags = 0x100, hwndTarget = src.Handle } };
            uint sz = (uint)Marshal.SizeOf<Native.RAWINPUTDEVICE>();
            Native.RegisterRawInputDevices(d, 1, sz);
            ready.Set();
            Dispatcher.Run();
            d[0].dwFlags = 0x1;
            d[0].hwndTarget = IntPtr.Zero;
            Native.RegisterRawInputDevices(d, 1, sz);
        }
        catch (Exception e)
        {
            Log.Write(e);
        }
        finally
        {
            try { src?.Dispose(); } catch { }
            src = null;
            Marshal.FreeHGlobal(mem);
            mem = IntPtr.Zero;
            ready.Set();
        }
    }

    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x00FF)
        {
            try { Process(l); } catch { }
        }
        return IntPtr.Zero;
    }

    void Process(IntPtr hRaw)
    {
        uint hs = (uint)(8 + 2 * IntPtr.Size);
        uint size = (uint)buf.Length;
        uint r = Native.GetRawInputData(hRaw, 0x10000003, mem, ref size, hs);
        if (r == uint.MaxValue || r < hs + 24) return;
        Marshal.Copy(mem, buf, 0, (int)Math.Min(r, (uint)buf.Length));
        if (BitConverter.ToUInt32(buf, 0) != 0) return;
        int o = (int)hs;
        ushort flags = BitConverter.ToUInt16(buf, o);
        ushort bf = BitConverter.ToUInt16(buf, o + 4);
        short bd = BitConverter.ToInt16(buf, o + 6);
        int lx = BitConverter.ToInt32(buf, o + 12);
        int ly = BitConverter.ToInt32(buf, o + 16);
        if ((flags & 1) == 0)
        {
            Interlocked.Add(ref dx, lx);
            Interlocked.Add(ref dy, ly);
        }
        Interlocked.Increment(ref events);
        if (lx != 0 || ly != 0 || bf != 0)
            Interlocked.Exchange(ref active, IntPtr.Size == 8 ? BitConverter.ToInt64(buf, 8) : BitConverter.ToInt32(buf, 8));
        int m = buttons;
        if ((bf & 0x1) != 0) m |= 1;
        if ((bf & 0x2) != 0) m &= ~1;
        if ((bf & 0x4) != 0) m |= 2;
        if ((bf & 0x8) != 0) m &= ~2;
        if ((bf & 0x10) != 0) m |= 4;
        if ((bf & 0x20) != 0) m &= ~4;
        if ((bf & 0x40) != 0) m |= 8;
        if ((bf & 0x80) != 0) m &= ~8;
        if ((bf & 0x100) != 0) m |= 16;
        if ((bf & 0x200) != 0) m &= ~16;
        buttons = m;
        if ((bf & 0x400) != 0) Interlocked.Add(ref wheel, bd);
    }
}
