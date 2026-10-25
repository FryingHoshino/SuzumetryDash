using System.Diagnostics;
using System.Text;

namespace Suzumetry.Core;

public struct PadState
{
    public double RawX, RawY, Nx, Ny, Hz, Speed;
    public int Buttons;
    public long Wheel;
    public long Device;
    public int Mode;
    public bool Missing;
    public DeviceDesc? Source;
}

public sealed class PadModel : IDisposable
{
    public struct P
    {
        public float X, Y;
        public long T;
    }

    readonly Settings st;
    readonly RawMouse mouse = new();
    readonly object gate = new();
    readonly object life = new();
    readonly P[] ring = new P[1024];
    int head, count, refs, prevButtons;
    double rx, ry, nx, ny, hz, speed;
    long wheelBase;
    int curSrc;
    bool missing;
    DeviceDesc? source;
    volatile bool resetReq;
    volatile bool run;
    Thread? thread;

    public PadModel(Settings settings) { st = settings; }

    public void Acquire()
    {
        lock (life)
        {
            if (++refs == 1) StartAll();
        }
    }

    public void Release()
    {
        lock (life)
        {
            if (refs > 0 && --refs == 0) StopAll();
        }
    }

    public void Reset() { resetReq = true; }

    public void Dispose()
    {
        lock (life)
        {
            refs = 0;
            StopAll();
        }
    }

    void StartAll()
    {
        mouse.Start();
        run = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "Suzumetry.Pad" };
        thread.Start();
    }

    void StopAll()
    {
        run = false;
        thread?.Join(2000);
        thread = null;
        mouse.Stop();
    }

    public PadState State()
    {
        lock (gate)
        {
            return new PadState
            {
                RawX = rx,
                RawY = ry,
                Nx = nx,
                Ny = ny,
                Hz = hz,
                Speed = speed,
                Buttons = curSrc == 0 ? mouse.Buttons : 0,
                Wheel = curSrc == 0 ? mouse.Wheel - wheelBase : 0,
                Device = mouse.Active,
                Mode = curSrc,
                Missing = missing,
                Source = source
            };
        }
    }

    public int CopyTrail(P[] dst, long now, long maxAge)
    {
        lock (gate)
        {
            int n = 0;
            int len = ring.Length;
            int start = ((head - count) % len + len) % len;
            for (int k = 0; k < count && n < dst.Length; k++)
            {
                var p = ring[(start + k) % len];
                if (now - p.T <= maxAge) dst[n++] = p;
            }
            return n;
        }
    }

    static double Dead(double v, double dz)
    {
        double a = Math.Abs(v);
        if (a <= dz) return 0;
        double r = (a - dz) / (1 - dz);
        return v < 0 ? -r : r;
    }

    void Loop()
    {
        int hzSet = st.PadHz;
        using var pacer = new Pacer(hzSet);
        var axes = new AxisInput();
        long last = Stopwatch.GetTimestamp();
        long lastPush = last, statT = last, statEv = mouse.Events, lastAxis = 0, lastDesc = 0, axisEv = 0;
        double path = 0, curX = 0, curY = 0, offX = 0, offY = 0, prevVx = 0, prevVy = 0;
        bool connected = false;
        int lastSrc = 0;
        DeviceDesc? desc = null;
        while (run)
        {
            try
            {
                int want = st.PadHz;
                if (want != hzSet)
                {
                    hzSet = want;
                    pacer.SetRate(hzSet);
                }
                long now = Stopwatch.GetTimestamp();
                double dt = Math.Min(0.1, (now - last) / (double)Stopwatch.Frequency);
                last = now;
                mouse.Take(out long dx, out long dy);
                int srcMode = st.PadSource;
                if (srcMode != lastSrc)
                {
                    lastSrc = srcMode;
                    offX = 0;
                    offY = 0;
                    curX = 0;
                    curY = 0;
                    prevVx = 0;
                    prevVy = 0;
                    connected = false;
                    desc = null;
                    lastAxis = 0;
                    lastDesc = 0;
                    axisEv = 0;
                    path = 0;
                    statEv = srcMode == 0 ? mouse.Events : 0;
                    axes.Reset();
                    lock (gate)
                    {
                        rx = 0;
                        ry = 0;
                        count = 0;
                    }
                }
                if (srcMode != 0)
                {
                    if (now - lastAxis >= Stopwatch.Frequency / 250)
                    {
                        lastAxis = now;
                        connected = axes.Poll(srcMode, st.PadDevice, st.PadAxisX, st.PadAxisY, out curX, out curY);
                        if (connected) axisEv++;
                    }
                    if (now - lastDesc >= Stopwatch.Frequency / 2)
                    {
                        lastDesc = now;
                        desc = AxisInput.Describe(srcMode, st.PadDevice);
                    }
                }
                else
                {
                    path += Math.Sqrt((double)dx * dx + (double)dy * dy);
                }
                double ddx = st.PadInvertX ? -dx : dx;
                double ddy = st.PadInvertY ? dy : -dy;
                int btn = mouse.Buttons;
                if (st.PadMidReset && (btn & 4) != 0 && (prevButtons & 4) == 0) resetReq = true;
                prevButtons = btn;
                lock (gate)
                {
                    curSrc = srcMode;
                    missing = srcMode != 0 && !connected;
                    source = srcMode != 0 ? desc : null;
                    if (resetReq)
                    {
                        resetReq = false;
                        rx = 0;
                        ry = 0;
                        count = 0;
                        wheelBase = mouse.Wheel;
                        offX = curX;
                        offY = curY;
                    }
                    double range = Math.Max(10, st.PadRange);
                    double sens = Math.Max(0.05, st.PadSens);
                    double lim = range / sens;
                    if (srcMode == 0)
                    {
                        rx = Math.Clamp(rx + ddx, -lim, lim);
                        ry = Math.Clamp(ry + ddy, -lim, lim);
                        if (st.PadCenter > 0)
                        {
                            double k = 1 - Math.Exp(-st.PadCenter * dt);
                            rx -= rx * k;
                            if (st.PadCenterY) ry -= ry * k;
                        }
                    }
                    else
                    {
                        double dz = st.PadDeadzone;
                        double vx = Dead(Math.Clamp(curX - offX, -1, 1), dz);
                        double vy = Dead(Math.Clamp(curY - offY, -1, 1), dz);
                        if (st.PadInvertX) vx = -vx;
                        if (st.PadInvertY) vy = -vy;
                        path += (Math.Abs(vx - prevVx) + Math.Abs(vy - prevVy)) * range;
                        prevVx = vx;
                        prevVy = vy;
                        rx = vx * range / sens;
                        ry = vy * range / sens;
                    }
                    nx = rx * sens / range;
                    ny = ry * sens / range;
                    if (now - lastPush >= Stopwatch.Frequency / 200)
                    {
                        lastPush = now;
                        ring[head] = new P { X = (float)nx, Y = (float)ny, T = now };
                        head = (head + 1) % ring.Length;
                        if (count < ring.Length) count++;
                    }
                    if (now - statT >= Stopwatch.Frequency / 4)
                    {
                        double el = (now - statT) / (double)Stopwatch.Frequency;
                        long ev = srcMode == 0 ? mouse.Events : axisEv;
                        hz = (ev - statEv) / el;
                        speed = path / el;
                        statEv = ev;
                        path = 0;
                        statT = now;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write(e);
            }
            pacer.Wait();
        }
    }
}

public static class PadText
{
    [ThreadStatic] static StringBuilder? sb;

    static string Btn(int m)
    {
        if (m == 0) return "-";
        var b = new StringBuilder(8);
        if ((m & 1) != 0) b.Append('L');
        if ((m & 2) != 0) b.Append('R');
        if ((m & 4) != 0) b.Append('M');
        if ((m & 8) != 0) b.Append('4');
        if ((m & 16) != 0) b.Append('5');
        return b.ToString();
    }

    public static string Data(PadState s, Snap? f, Settings st)
    {
        var b = sb ??= new StringBuilder(200);
        b.Clear();
        if (st.PadRaw)
            b.Append("RAW  X ").Append(((int)s.RawX).ToString("+0;-0;0")).Append("  Y ").Append(((int)s.RawY).ToString("+0;-0;0")).Append('\n');
        else
            b.Append("POS  X ").Append(s.Nx.ToString("+0.00;-0.00;0.00")).Append("  Y ").Append(s.Ny.ToString("+0.00;-0.00;0.00")).Append('\n');
        if (st.PadGame && f != null)
        {
            if (f.Game == Game.DR2) b.Append("STEER ").Append(f.Steer.ToString("+0.00;-0.00;0.00")).Append('\n');
            b.Append("THR ").Append((int)(f.Throttle * 100)).Append("%  BRK ").Append((int)(f.Brake * 100)).Append("%  CLT ").Append((int)(f.Clutch * 100)).Append('%');
        }
        return b.ToString().TrimEnd('\n');
    }

    public static string Extra(PadState s, Settings st)
    {
        if (!st.PadExtra) return "";
        var b = sb ??= new StringBuilder(200);
        b.Clear();
        b.Append(((int)s.Hz).ToString()).Append(" Hz  ").Append(((int)s.Speed).ToString()).Append(" cnt/s\n");
        b.Append("BTN ").Append(Btn(s.Buttons)).Append("  WHL ").Append((s.Wheel / 120).ToString("+0;-0;0"));
        return b.ToString();
    }
}
