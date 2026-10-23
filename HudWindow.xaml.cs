using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Suzumetry.Core;

namespace Suzumetry;

public partial class HudWindow : Window
{
    static readonly Brush Cold = Make(0x4D, 0xA6, 0xFF);
    static readonly Brush Good = Make(0x5C, 0xFF, 0x8A);
    static readonly Brush Hot = Make(0xFF, 0x5A, 0x5A);
    static readonly Brush Idle = Make(0x7F, 0x87, 0x98);
    static readonly Brush Accent = Make(0xFF, 0x5A, 0x7A);
    static readonly Brush[] Flags =
    {
        Brushes.Transparent, Make(0x3A, 0x7B, 0xFF), Make(0xFF, 0xD2, 0x1F), Make(0x9A, 0xA0, 0xAD),
        Make(0xF1, 0xF3, 0xF8), Make(0xF1, 0xF3, 0xF8), Make(0xFF, 0x8A, 0x1F), Make(0x2B, 0xD9, 0x6B), Make(0xFF, 0x8A, 0x1F)
    };

    readonly Hub hub;
    readonly Settings st;
    readonly TextBlock[] tyres;
    readonly Brush[] tyreBrush = new Brush[4];
    readonly int[] tyreVal = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
    readonly DispatcherTimer topTimer;
    IntPtr hwnd;
    bool locked = true;
    bool shown = true;
    bool redline;
    bool closed;
    long lastSeq = -1;
    int lastGear = int.MinValue, lastSpeed = -1, lastRpmBucket = -1, lastPos = -1, lastCur = -1, lastLast = -1, lastBest = -1, lastFuel = -1, lastFlag = -1;
    string lastState = "";

    static Brush Make(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public HudWindow(Hub hub, Settings st)
    {
        InitializeComponent();
        this.hub = hub;
        this.st = st;
        tyres = new[] { T0, T1, T2, T3 };
        var vl = SystemParameters.VirtualScreenLeft;
        var vt = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth;
        var vh = SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(st.HudX, vl, Math.Max(vl, vl + vw - 120));
        Top = Math.Clamp(st.HudY, vt, Math.Max(vt, vt + vh - 120));
        ApplySettings();
        MouseLeftButtonDown += OnDrag;
        topTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        topTimer.Tick += (_, _) => Reassert();
        topTimer.Start();
        CompositionTarget.Rendering += OnRender;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        ApplyLock();
    }

    void Reassert()
    {
        if (hwnd == IntPtr.Zero || !IsVisible) return;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    void ApplyLock()
    {
        if (hwnd == IntPtr.Zero) return;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        if (locked) ex |= Native.WS_EX_TRANSPARENT;
        else ex &= ~Native.WS_EX_TRANSPARENT;
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
        Root.BorderBrush = locked ? Brushes.Transparent : Accent;
    }

    public void SetLocked(bool value)
    {
        locked = value;
        ApplyLock();
        if (!locked) SetShown(true);
    }

    public void ApplySettings()
    {
        Scale.ScaleX = st.HudScale;
        Scale.ScaleY = st.HudScale;
        Root.Opacity = st.HudOpacity;
        PedalP.Visibility = st.HudPedals ? Visibility.Visible : Visibility.Collapsed;
        TyreP.Visibility = st.HudTyres ? Visibility.Visible : Visibility.Collapsed;
        LapP.Visibility = st.HudLaps ? Visibility.Visible : Visibility.Collapsed;
        FuelT.Visibility = st.HudFuel ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Shutdown()
    {
        if (closed) return;
        closed = true;
        CompositionTarget.Rendering -= OnRender;
        topTimer.Stop();
        try { Close(); } catch { }
    }

    void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (locked) return;
        try { DragMove(); } catch { return; }
        st.HudX = Left;
        st.HudY = Top;
        st.Save();
    }

    void SetShown(bool v)
    {
        if (v == shown) return;
        shown = v;
        Root.Visibility = v ? Visibility.Visible : Visibility.Hidden;
    }

    void OnRender(object? sender, EventArgs e)
    {
        if (closed || !IsVisible) return;
        var f = hub.Current;
        bool live = f != null && hub.Active && f.Status != 0 && f.Status != 1;
        SetShown(live || !locked);
        if (f == null || f.Seq == lastSeq) return;
        lastSeq = f.Seq;
        if (!live) return;
        Update(f);
    }

    static double C01(float v) => v < 0f ? 0d : v > 1f ? 1d : v;

    static void Set(TextBlock t, string v)
    {
        if (t.Text != v) t.Text = v;
    }

    void Update(Snap f)
    {
        if (f.Gear != lastGear)
        {
            lastGear = f.Gear;
            GearT.Text = f.Gear < 0 ? "R" : f.Gear == 0 ? "N" : f.Gear.ToString();
        }
        int sp = (int)f.Speed;
        if (sp != lastSpeed)
        {
            lastSpeed = sp;
            SpeedT.Text = sp.ToString();
        }
        double r = f.MaxRpm > 0 ? Math.Clamp((double)f.Rpm / f.MaxRpm, 0, 1) : 0;
        RpmS.ScaleX = r;
        bool red = r > 0.94;
        if (red != redline)
        {
            redline = red;
            RpmFill.Fill = red ? Hot : Good;
        }
        int rb = f.Rpm / 50;
        if (rb != lastRpmBucket)
        {
            lastRpmBucket = rb;
            RpmT.Text = f.Rpm + " rpm";
        }
        if (f.Position != lastPos)
        {
            lastPos = f.Position;
            PosT.Text = f.Position > 0 ? "P" + f.Position : "";
        }
        string state = f.PitLimiter ? "LIMITER" : f.InPit ? "PIT" : "";
        if (state != lastState)
        {
            lastState = state;
            StateT.Text = state;
        }
        int fl = f.Flag;
        if (fl != lastFlag)
        {
            lastFlag = fl;
            FlagR.Fill = fl > 0 && fl < Flags.Length ? Flags[fl] : Brushes.Transparent;
        }
        if (st.HudPedals)
        {
            ThrS.ScaleY = C01(f.Throttle);
            BrkS.ScaleY = C01(f.Brake);
            CltS.ScaleY = C01(f.Clutch);
        }
        if (st.HudTyres)
        {
            bool hasTyre = f.TyreTemp[0] > 0 || f.TyreTemp[1] > 0 || f.TyreTemp[2] > 0 || f.TyreTemp[3] > 0;
            var src = hasTyre ? f.TyreTemp : f.BrakeTemp;
            for (int i = 0; i < 4; i++)
            {
                int v = (int)src[i];
                if (v == tyreVal[i]) continue;
                tyreVal[i] = v;
                tyres[i].Text = v > 0 ? v + "°" : "--";
                Brush b = v <= 0 ? Idle : !hasTyre ? Idle : v < 60 ? Cold : v < 105 ? Good : Hot;
                if (!ReferenceEquals(b, tyreBrush[i]))
                {
                    tyreBrush[i] = b;
                    tyres[i].Foreground = b;
                }
            }
        }
        if (st.HudLaps)
        {
            int c = f.CurMs / 50;
            if (c != lastCur)
            {
                lastCur = c;
                CurT.Text = Fmt.Time(f.CurMs);
            }
            if (f.LastMs != lastLast)
            {
                lastLast = f.LastMs;
                LastT.Text = "LAST " + Fmt.Time(f.LastMs);
            }
            if (f.BestMs != lastBest)
            {
                lastBest = f.BestMs;
                BestT.Text = "BEST " + Fmt.Time(f.BestMs);
            }
        }
        if (st.HudFuel)
        {
            int fu = (int)(f.Fuel * 10);
            if (fu != lastFuel)
            {
                lastFuel = fu;
                FuelT.Text = f.Fuel > 0 ? "FUEL " + f.Fuel.ToString("0.0") + " L" + (f.FuelPerLap > 0 ? "   " + f.FuelPerLap.ToString("0.00") + " /lap" : "") : "";
            }
        }
    }
}
