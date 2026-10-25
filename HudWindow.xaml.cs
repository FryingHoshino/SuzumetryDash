using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    static readonly Brush Accent = Make(0xFF, 0x5A, 0x7A);
    static readonly Brush[] Flags =
    {
        Brushes.Transparent, Make(0x3A, 0x7B, 0xFF), Make(0xFF, 0xD2, 0x1F), Make(0x9A, 0xA0, 0xAD),
        Make(0xF1, 0xF3, 0xF8), Make(0xF1, 0xF3, 0xF8), Make(0xFF, 0x8A, 0x1F), Make(0x2B, 0xD9, 0x6B), Make(0xFF, 0x8A, 0x1F)
    };

    readonly Hub hub;
    readonly Settings st;
    readonly PadModel pad;
    readonly Border[] brkTile = new Border[4];
    readonly Border[] tyrTile = new Border[4];
    readonly TextBlock[] brkTxt = new TextBlock[4];
    readonly TextBlock[] tyrTxt = new TextBlock[4];
    readonly Brush?[] brkRef = new Brush?[4];
    readonly Brush?[] tyrRef = new Brush?[4];
    readonly int[] brkVal = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
    readonly int[] tyrVal = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
    readonly StringBuilder extraSb = new(256);
    readonly bool[] warnBrk = new bool[4];
    readonly bool[] warnTyr = new bool[4];
    Border[] leds = Array.Empty<Border>();
    Brush ledOff = Brushes.Transparent, ledLow = Brushes.Transparent, ledMid = Brushes.Transparent, ledHigh = Brushes.Transparent;
    bool warnLim, warnFuel, shiftFlash;
    int shiftLit = -1;
    bool lastFast = true;
    readonly DispatcherTimer topTimer;
    Brush rpmB = Brushes.Transparent, redB = Brushes.Transparent;
    Brush coldB = Brushes.Transparent, goodB = Brushes.Transparent, hotB = Brushes.Transparent, idleB = Brushes.Transparent;
    Brush coldBg = Brushes.Transparent, goodBg = Brushes.Transparent, hotBg = Brushes.Transparent, idleBg = Brushes.Transparent;
    Brush brakeBgB = Brushes.Transparent, tyreTextB = Brushes.Transparent;
    IntPtr hwnd;
    bool locked = true;
    bool shown = true;
    bool redline;
    bool closed;
    bool preview;
    bool previewDirty;
    int previewMode;
    bool seenTyre, seenBrake, seenCur, seenLast, seenBest;
    Game seenGame = Game.None;
    string? seenTrack;
    int lastMode = -1;
    int lastExtra = int.MinValue;
    long lastSeq = -1;
    int lastGear = int.MinValue, lastSpeed = -1, lastRpmBucket = -1, lastPos = -1, lastCur = -1, lastLast = -1, lastBest = -1, lastFlag = -1;
    int lastInfo = int.MinValue, steerState = -1;
    string? lastCar, lastTrack;
    string lastState = "";
    long lastDraw, frameTicks, lastPadText;
    int padVis = -1;
    string lastPadStr = "";
    string lastPadExtra = "";
    string? lastCapHead, lastCapDetail;
    bool padHeld;

    static Brush Make(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    static Brush B(string hex, double opacity) => PadStyle.MakeBrush(hex, opacity);

    public int PreviewMode
    {
        get => previewMode;
        set
        {
            previewMode = value;
            ResetSeen();
            lastMode = -1;
            previewDirty = true;
        }
    }

    public HudWindow(Hub hub, Settings st, PadModel pad)
    {
        InitializeComponent();
        this.hub = hub;
        this.st = st;
        this.pad = pad;
        Pad.Bind(st, pad);
        BuildTyreCells();
        var vl = SystemParameters.VirtualScreenLeft;
        var vt = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth;
        var vh = SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(st.HudX, vl, Math.Max(vl, vl + vw - 120));
        Top = Math.Clamp(st.HudY, vt, Math.Max(vt, vt + vh - 120));
        ApplySettings();
        MouseLeftButtonDown += OnDrag;
        Body.LayoutUpdated += (_, _) => FitText();
        topTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        topTimer.Tick += (_, _) => Reassert();
        topTimer.Start();
        CompositionTarget.Rendering += OnRender;
    }

    void BuildTyreCells()
    {
        for (int i = 0; i < 4; i++)
        {
            var cell = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 4) };
            brkTxt[i] = new TextBlock { Text = "--", FontSize = 15, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            brkTile[i] = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 2, 6, 2), MinWidth = 44, Child = brkTxt[i] };
            tyrTxt[i] = new TextBlock { Text = "--", FontSize = 15, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            tyrTile[i] = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 2, 6, 2), MinWidth = 44, Child = tyrTxt[i] };
            cell.Children.Add(brkTile[i]);
            cell.Children.Add(tyrTile[i]);
            TyreGrid.Children.Add(cell);
        }
    }

    void FitText()
    {
        double w = Math.Max(Math.Max(MainRow.DesiredSize.Width, LowRow.DesiredSize.Width), 160);
        if (!(Math.Abs(HeadT.Width - w) < 0.5)) HeadT.Width = w;
        if (!(Math.Abs(FuelT.MaxWidth - w) < 0.5)) FuelT.MaxWidth = w;
        if (!(Math.Abs(ExtraT.MaxWidth - w) < 0.5)) ExtraT.MaxWidth = w;
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
        CoordCard.BorderBrush = locked ? Brushes.Transparent : Accent;
    }

    public void SetLocked(bool value)
    {
        locked = value;
        ApplyLock();
        if (!locked) SetShown(true);
    }

    public void SetPreview(bool on)
    {
        preview = on;
        ResetSeen();
        lastMode = -1;
        previewDirty = true;
    }

    void ResetSeen()
    {
        seenTyre = seenBrake = seenCur = seenLast = seenBest = false;
    }

    public void ApplySettings()
    {
        Scale.ScaleX = st.HudScale;
        Scale.ScaleY = st.HudScale;
        HudDock.Opacity = st.HudOpacity;
        frameTicks = (long)(Stopwatch.Frequency / (double)Math.Clamp(st.HudFps, 10, 240) * 0.92);
        int side = st.PadSide;
        DockPanel.SetDock(CoordCard, side switch { 0 => Dock.Left, 2 => Dock.Top, 3 => Dock.Bottom, _ => Dock.Right });
        CoordCard.Margin = side switch { 0 => new Thickness(0, 0, 8, 0), 2 => new Thickness(0, 0, 0, 8), 3 => new Thickness(0, 8, 0, 0), _ => new Thickness(8, 0, 0, 0) };
        CoordCard.HorizontalAlignment = side >= 2 ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        if (st.HudPad && !padHeld)
        {
            pad.Acquire();
            padHeld = true;
        }
        else if (!st.HudPad && padHeld)
        {
            pad.Release();
            padHeld = false;
        }
        padVis = -1;
        lastPadStr = "";
        lastPadExtra = "";
        lastCapHead = null;
        lastCapDetail = null;
        ApplyPadStyle();
        ApplyStyle();
        BuildShift();
        PedalP.Visibility = Vis(st.HudPedals);
        LapP.Visibility = Vis(st.HudLaps);
        GBox.Visibility = Vis(st.HudGForce);
        HeadT.Visibility = Vis(st.HudHeader);
        ProgG.Visibility = Vis(st.HudProgress);
        FuelT.Visibility = Vis(st.HudFuel || st.HudWeather);
        ExtraBox.Visibility = Vis(st.HudExtra);
        TyreBox.Visibility = Visibility.Collapsed;
        UnitT.Text = st.HudMph ? "mph" : "km/h";
        lastSpeed = -1;
        lastInfo = int.MinValue;
        lastExtra = int.MinValue;
        lastMode = -1;
        steerState = -1;
        lastPos = -1;
        lastCar = null;
        lastSeq = -1;
        previewDirty = true;
    }

    static Visibility Vis(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

    void ApplyPadStyle()
    {
        CoordCard.Background = B(st.PadCardColor, st.PadCardOpacity);
        PadCapT.Foreground = B(st.PadCapColor, st.PadCapOpacity);
        PadCapT.FontSize = st.PadCapSize;
        PadCapT.MaxWidth = st.PadSize;
        PadDevT.Foreground = B(st.PadCapColor, st.PadCapOpacity);
        PadDevT.FontSize = Math.Max(6, st.PadCapSize - 1);
        PadDevT.MaxWidth = st.PadSize;
        PadTextT.Foreground = B(st.PadTextColor, st.PadTextOpacity);
        PadTextT.FontSize = st.PadTextSize;
        PadExtraT.Foreground = B(st.PadExtraColor, st.PadExtraOpacity);
        PadExtraT.FontSize = st.PadExtraSize;
    }

    void ApplyStyle()
    {
        Root.Background = B(st.HsBgColor, st.HsBgOpacity);
        HeadT.Foreground = B(st.HsHeadColor, st.HsHeadOpacity);
        GearT.Foreground = B(st.HsGearColor, st.HsGearOpacity);
        SpeedT.Foreground = B(st.HsSpeedColor, st.HsSpeedOpacity);
        UnitT.Foreground = B(st.HsUnitColor, st.HsUnitOpacity);
        RpmT.Foreground = B(st.HsRpmTextColor, st.HsRpmTextOpacity);
        RpmTrackR.Fill = B(st.HsRpmTrackColor, st.HsRpmTrackOpacity);
        rpmB = B(st.HsRpmColor, st.HsRpmOpacity);
        redB = B(st.HsRedlineColor, st.HsRedlineOpacity);
        RpmFill.Fill = redline ? redB : rpmB;
        PosT.Foreground = B(st.HsPosColor, st.HsPosOpacity);
        StateT.Foreground = B(st.HsStateColor, st.HsStateOpacity);
        SteerTrackR.Fill = B(st.HsSteerTrackColor, st.HsSteerTrackOpacity);
        SteerCenterR.Fill = B(st.HsSteerCenterColor, st.HsSteerCenterOpacity);
        SteerMarkR.Fill = B(st.HsSteerMarkColor, st.HsSteerMarkOpacity);
        ProgTrackR.Fill = B(st.HsProgTrackColor, st.HsProgTrackOpacity);
        ProgFill.Fill = B(st.HsProgColor, st.HsProgOpacity);
        var pedalTrack = B(st.HsPedalTrackColor, st.HsPedalTrackOpacity);
        CltTrk.Fill = pedalTrack;
        BrkTrk.Fill = pedalTrack;
        ThrTrk.Fill = pedalTrack;
        CltFill.Fill = B(st.HsCltColor, st.HsCltOpacity);
        BrkFill.Fill = B(st.HsBrkColor, st.HsBrkOpacity);
        ThrFill.Fill = B(st.HsThrColor, st.HsThrOpacity);
        TyreCap.Foreground = B(st.HsTempCapColor, st.HsTempCapOpacity);
        coldB = B(st.HsColdColor, st.HsColdOpacity);
        goodB = B(st.HsGoodColor, st.HsGoodOpacity);
        hotB = B(st.HsHotColor, st.HsHotOpacity);
        idleB = B(st.HsIdleColor, st.HsIdleOpacity);
        coldBg = B(st.HsColdColor, st.HsColdOpacity * st.HsTyreBgOpacity);
        goodBg = B(st.HsGoodColor, st.HsGoodOpacity * st.HsTyreBgOpacity);
        hotBg = B(st.HsHotColor, st.HsHotOpacity * st.HsTyreBgOpacity);
        idleBg = B(st.HsIdleColor, st.HsIdleOpacity * st.HsTyreBgOpacity);
        brakeBgB = B(st.HsBrakeBgColor, st.HsBrakeBgOpacity);
        tyreTextB = B(st.HsTyreTextColor, st.HsTyreTextOpacity);
        for (int i = 0; i < 4; i++)
        {
            brkTile[i].Background = brakeBgB;
            tyrTxt[i].Foreground = tyreTextB;
            brkVal[i] = int.MinValue;
            tyrVal[i] = int.MinValue;
            brkRef[i] = null;
            tyrRef[i] = null;
        }
        var ring = B(st.HsGRingColor, st.HsGRingOpacity);
        GRingO.Stroke = ring;
        GRingI.Stroke = ring;
        GDot.Fill = B(st.HsGDotColor, st.HsGDotOpacity);
        CurT.Foreground = B(st.HsCurColor, st.HsCurOpacity);
        LastT.Foreground = B(st.HsLastColor, st.HsLastOpacity);
        BestT.Foreground = B(st.HsBestColor, st.HsBestOpacity);
        FuelT.Foreground = B(st.HsInfoColor, st.HsInfoOpacity);
        ExtraT.Foreground = B(st.HsExtraTextColor, st.HsExtraTextOpacity);
        ExtraT.FontSize = st.HsExtraSize;
        ledOff = B(st.HsShiftOffColor, st.HsShiftOffOpacity);
        ledLow = B(st.HsShiftLowColor, st.HsShiftLowOpacity);
        ledMid = B(st.HsShiftMidColor, st.HsShiftMidOpacity);
        ledHigh = B(st.HsShiftHighColor, st.HsShiftHighOpacity);
        lastFlag = -1;
    }

    void BuildShift()
    {
        int n = (int)Math.Round(Math.Clamp(st.HsShiftCount, 4, 24));
        ShiftGrid.Height = st.HsShiftHeight;
        if (leds.Length != n)
        {
            ShiftGrid.Children.Clear();
            leds = new Border[n];
            for (int i = 0; i < n; i++)
            {
                leds[i] = new Border { CornerRadius = new CornerRadius(3), Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0) };
                ShiftGrid.Children.Add(leds[i]);
            }
        }
        shiftLit = -1;
        shiftFlash = false;
        PaintShift(true);
    }

    void PaintShift(bool flashOn)
    {
        int n = leds.Length;
        for (int i = 0; i < n; i++)
        {
            Brush b;
            if (shiftFlash) b = flashOn ? ledHigh : ledOff;
            else if (i < shiftLit)
            {
                double p = (double)i / n;
                b = p < 0.5 ? ledLow : p < 0.8 ? ledMid : ledHigh;
            }
            else b = ledOff;
            leds[i].Background = b;
        }
    }

    void TickBlink(long now)
    {
        bool slow = (now / (Stopwatch.Frequency / 8)) % 2 == 0;
        bool fast = (now / (Stopwatch.Frequency / 16)) % 2 == 0;
        if (shiftFlash && fast != lastFast) PaintShift(fast);
        lastFast = fast;
        for (int i = 0; i < 4; i++)
        {
            double ob = warnBrk[i] && !slow ? 0.25 : 1;
            if (brkTile[i].Opacity != ob) brkTile[i].Opacity = ob;
            double ot = warnTyr[i] && !slow ? 0.25 : 1;
            if (tyrTile[i].Opacity != ot) tyrTile[i].Opacity = ot;
        }
        double ol = warnLim && !slow ? 0.25 : 1;
        if (StateT.Opacity != ol) StateT.Opacity = ol;
        double of2 = warnFuel && !slow ? 0.25 : 1;
        if (FuelT.Opacity != of2) FuelT.Opacity = of2;
    }

    public void Shutdown()
    {
        if (closed) return;
        closed = true;
        if (padHeld)
        {
            pad.Release();
            padHeld = false;
        }
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

    Snap MakeSample()
    {
        var s = new Snap
        {
            Game = Game.DR2,
            Car = "Preview car",
            Track = "Preview stage",
            Speed = 142f,
            Throttle = 0.8f,
            Brake = 0.15f,
            Clutch = 0f,
            Steer = 0.25f,
            Gear = 3,
            GLat = 0.6f,
            GLon = -0.4f,
            Lap = 0,
            Rpm = 6200,
            MaxRpm = 8000,
            Position = 2,
            Fuel = 32f,
            MaxFuel = 60f,
            Sector = 1,
            CurMs = 83450,
            LastMs = 85120,
            BestMs = 84010,
            LapPos = 0.42f
        };
        float[] tt = { 62f, 88f, 128f, 95f };
        float[] bt = { 90f, 340f, 760f, 180f };
        for (int i = 0; i < 4; i++)
        {
            if (previewMode == 0 || previewMode == 1) s.TyreTemp[i] = tt[i];
            if (previewMode == 0 || previewMode == 2) s.BrakeTemp[i] = bt[i];
            s.TyrePress[i] = 29.5f + i;
        }
        return s;
    }

    void OnRender(object? sender, EventArgs e)
    {
        if (closed || !IsVisible) return;
        long now = Stopwatch.GetTimestamp();
        if (now - lastDraw < frameTicks) return;
        lastDraw = now;
        var f = hub.Current;
        bool live = f != null && hub.Active && f.Status != 0 && f.Status != 1;
        SetShown(live || preview || !locked);
        TickBlink(now);
        UpdatePad(live ? f : null, live || preview, now);
        if (!live && preview)
        {
            if (previewDirty)
            {
                previewDirty = false;
                Update(MakeSample());
            }
            return;
        }
        if (f == null || f.Seq == lastSeq) return;
        lastSeq = f.Seq;
        if (!live) return;
        Update(f);
    }

    void UpdatePad(Snap? f, bool live, long now)
    {
        bool want = st.HudPad && (live || st.PadAlways || !locked);
        int v = want ? 1 : 0;
        if (v != padVis)
        {
            padVis = v;
            CoordCard.Visibility = Vis(want);
        }
        if (!want) return;
        Pad.Live = f;
        Pad.Refresh();
        if (now - lastPadText < Stopwatch.Frequency / 20) return;
        lastPadText = now;
        var ps = pad.State();
        string t = PadText.Data(ps, f, st);
        if (t != lastPadStr)
        {
            lastPadStr = t;
            PadTextT.Text = t;
        }
        string x = PadText.Extra(ps, st);
        if (x != lastPadExtra)
        {
            lastPadExtra = x;
            PadExtraT.Text = x;
            PadExtraT.Visibility = Vis(x.Length > 0);
        }
        InputDevices.Caption(ps, st, out string ch, out string cd);
        if (ch != lastCapHead)
        {
            lastCapHead = ch;
            PadCapT.Text = ch;
        }
        if (cd != lastCapDetail)
        {
            lastCapDetail = cd;
            PadDevT.Text = cd;
            PadDevT.Visibility = Vis(cd.Length > 0);
        }
    }

    static double C01(float v) => v < 0f ? 0d : v > 1f ? 1d : v;

    static void Set(TextBlock t, string v)
    {
        if (t.Text != v) t.Text = v;
    }

    static string P(float v) => v > 0 ? v.ToString("0.0", CultureInfo.InvariantCulture) : "null";

    string BuildExtra(Snap f)
    {
        var ic = CultureInfo.InvariantCulture;
        var b = extraSb;
        b.Clear();
        b.Append("THR ").Append((int)(C01(f.Throttle) * 100)).Append("%  BRK ").Append((int)(C01(f.Brake) * 100)).Append("%  CLT ").Append((int)(C01(f.Clutch) * 100)).Append('%').Append('\n');
        b.Append("STEER ").Append(f.Game == Game.DR2 ? f.Steer.ToString("+0.00;-0.00;0.00", ic) : "null").Append("  SEC ").Append(f.Sector).Append("  LAP ").Append(f.Lap + 1).Append('\n');
        b.Append("PSI  FL ").Append(P(f.TyrePress[0])).Append("  FR ").Append(P(f.TyrePress[1])).Append("  RL ").Append(P(f.TyrePress[2])).Append("  RR ").Append(P(f.TyrePress[3])).Append('\n');
        b.Append("G  LAT ").Append(f.GLat.ToString("+0.00;-0.00;0.00", ic)).Append("  LON ").Append(f.GLon.ToString("+0.00;-0.00;0.00", ic)).Append('\n');
        b.Append("TANK ").Append(f.Fuel > 0 ? f.Fuel.ToString("0.0", ic) : "null").Append(" / ").Append(f.MaxFuel > 0 ? f.MaxFuel.ToString("0.0", ic) : "null").Append(" L");
        return b.ToString();
    }

    void ApplyMode(int mode)
    {
        lastMode = mode;
        TyreBox.Visibility = Vis(st.HudTyres && mode != 0);
        bool t = (mode & 1) != 0, b = (mode & 2) != 0;
        for (int i = 0; i < 4; i++)
        {
            brkTile[i].Visibility = Vis(b);
            tyrTile[i].Visibility = Vis(t);
            brkTile[i].Margin = new Thickness(0, 0, t && b ? 4 : 0, 0);
            brkVal[i] = int.MinValue;
            tyrVal[i] = int.MinValue;
        }
        TyreCap.Text = t && b ? "BRAKE  |  TYRE °C" : t ? "TYRE °C" : "BRAKE °C";
    }

    void Update(Snap f)
    {
        for (int i = 0; i < 4; i++)
        {
            warnBrk[i] = false;
            warnTyr[i] = false;
        }
        warnLim = st.HudWarn && f.PitLimiter;
        warnFuel = st.HudWarn && st.HudFuel && f.Fuel > 0 && f.Fuel < st.HsFuelWarn;
        if (f.Game != seenGame || !ReferenceEquals(f.Track, seenTrack))
        {
            seenGame = f.Game;
            seenTrack = f.Track;
            ResetSeen();
        }
        if (!ReferenceEquals(f.Car, lastCar) || !ReferenceEquals(f.Track, lastTrack))
        {
            lastCar = f.Car;
            lastTrack = f.Track;
            Set(HeadT, f.Track.Length > 0 ? f.Car + " · " + f.Track : f.Car);
        }
        if (f.Gear != lastGear)
        {
            lastGear = f.Gear;
            GearT.Text = f.Gear < 0 ? "R" : f.Gear == 0 ? "N" : f.Gear.ToString();
        }
        int sp = (int)(st.HudMph ? f.Speed * 0.621371f : f.Speed);
        if (sp != lastSpeed)
        {
            lastSpeed = sp;
            SpeedT.Text = sp.ToString();
        }
        bool hasRpm = f.MaxRpm > 0 || f.Rpm > 0;
        RpmBarG.Visibility = Vis(hasRpm);
        RpmT.Visibility = Vis(hasRpm);
        double r = f.MaxRpm > 0 ? Math.Clamp((double)f.Rpm / f.MaxRpm, 0, 1) : 0;
        RpmS.ScaleX = r;
        bool red = r > 0.94;
        if (red != redline)
        {
            redline = red;
            RpmFill.Fill = red ? redB : rpmB;
        }
        bool showShift = st.HudShift && hasRpm;
        ShiftGrid.Visibility = Vis(showShift);
        if (showShift)
        {
            double span = Math.Max(0.05, 1.0 - st.HsShiftStart);
            int lit = (int)Math.Round(Math.Clamp((r - st.HsShiftStart) / span, 0, 1) * leds.Length);
            bool over = r >= st.HsShiftFlash;
            if (lit != shiftLit || over != shiftFlash)
            {
                shiftLit = lit;
                shiftFlash = over;
                PaintShift(lastFast);
            }
        }
        else shiftFlash = false;
        int rb = f.Rpm / 50;
        if (rb != lastRpmBucket)
        {
            lastRpmBucket = rb;
            RpmT.Text = f.Rpm + " rpm";
        }
        bool dr2 = f.Game == Game.DR2;
        int pct = (int)(f.LapPos * 100);
        int pk = dr2 ? 1000 + pct : f.Position;
        if (pk != lastPos)
        {
            lastPos = pk;
            PosT.Text = dr2 ? pct + "%" : f.Position > 0 ? "P" + f.Position : "";
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
        bool showPos = st.HudPos && PosT.Text.Length > 0;
        bool showState = state.Length > 0;
        bool showFlag = fl > 0 && fl < Flags.Length;
        PosT.Visibility = Vis(showPos);
        StateT.Visibility = Vis(showState);
        FlagR.Visibility = Vis(showFlag);
        PosCol.Visibility = Vis(showPos || showState || showFlag);
        if (st.HudProgress) ProgS.ScaleX = Math.Clamp(f.LapPos, 0f, 1f);
        int ss = st.HudSteer && dr2 ? 1 : 0;
        if (ss != steerState)
        {
            steerState = ss;
            SteerG.Visibility = Vis(ss == 1);
        }
        if (ss == 1) SteerT.X = Math.Clamp(f.Steer, -1f, 1f) * Math.Max(0.0, (SteerG.ActualWidth - 14.0) / 2.0);
        if (st.HudGForce)
        {
            GDotT.X = Math.Clamp(f.GLat / 2f, -1f, 1f) * 23.0;
            GDotT.Y = -Math.Clamp(f.GLon / 2f, -1f, 1f) * 23.0;
        }
        if (st.HudPedals)
        {
            ThrS.ScaleY = C01(f.Throttle);
            BrkS.ScaleY = C01(f.Brake);
            CltS.ScaleY = C01(f.Clutch);
        }
        if (st.HudTyres)
        {
            bool ht = false, hb = false;
            for (int i = 0; i < 4; i++)
            {
                if (f.TyreTemp[i] > 0) ht = true;
                if (f.BrakeTemp[i] > 0) hb = true;
            }
            if (ht) seenTyre = true;
            if (hb) seenBrake = true;
            int mode = (seenTyre ? 1 : 0) | (seenBrake ? 2 : 0);
            if (mode != lastMode) ApplyMode(mode);
            if (mode != 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if ((mode & 1) != 0)
                    {
                        int tv = (int)f.TyreTemp[i];
                        warnTyr[i] = st.HudWarn && tv >= st.HsTyreWarn;
                        if (tv != tyrVal[i])
                        {
                            tyrVal[i] = tv;
                            tyrTxt[i].Text = tv > 0 ? tv + "°" : "--";
                            Brush bg = tv <= 0 ? idleBg : tv < st.HsTyreCold ? coldBg : tv < st.HsTyreHot ? goodBg : hotBg;
                            if (!ReferenceEquals(bg, tyrRef[i]))
                            {
                                tyrRef[i] = bg;
                                tyrTile[i].Background = bg;
                            }
                        }
                    }
                    if ((mode & 2) != 0)
                    {
                        int bv = (int)f.BrakeTemp[i];
                        warnBrk[i] = st.HudWarn && bv >= st.HsBrakeWarn;
                        if (bv != brkVal[i])
                        {
                            brkVal[i] = bv;
                            brkTxt[i].Text = bv > 0 ? bv + "°" : "--";
                            Brush fg = bv <= 0 ? idleB : bv < st.HsBrakeCold ? coldB : bv < st.HsBrakeHot ? goodB : hotB;
                            if (!ReferenceEquals(fg, brkRef[i]))
                            {
                                brkRef[i] = fg;
                                brkTxt[i].Foreground = fg;
                            }
                        }
                    }
                }
            }
        }
        if (st.HudLaps)
        {
            if (f.CurMs > 0) seenCur = true;
            if (f.LastMs > 0) seenLast = true;
            if (f.BestMs > 0) seenBest = true;
            CurT.Visibility = Vis(seenCur);
            LastT.Visibility = Vis(seenLast);
            BestT.Visibility = Vis(seenBest);
            LapP.Visibility = Vis(seenCur || seenLast || seenBest);
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
        if (st.HudFuel || st.HudWeather)
        {
            int fu = (int)(f.Fuel * 10), fpl = (int)(f.FuelPerLap * 100), at = (int)f.AirTemp, rt = (int)f.RoadTemp;
            int key = HashCode.Combine(fu, fpl, at, rt);
            if (key != lastInfo)
            {
                lastInfo = key;
                var parts = new List<string>(2);
                if (st.HudFuel && f.Fuel > 0)
                    parts.Add("FUEL " + f.Fuel.ToString("0.0") + " L" + (f.FuelPerLap > 0 ? "  " + f.FuelPerLap.ToString("0.00") + "/lap" : ""));
                if (st.HudWeather && (f.AirTemp > 0 || f.RoadTemp > 0))
                    parts.Add("AIR " + at + "°  ROAD " + rt + "°");
                var text = string.Join("   ", parts);
                FuelT.Text = text;
                FuelT.Visibility = Vis(text.Length > 0);
            }
        }
        if (st.HudExtra)
        {
            var h = new HashCode();
            h.Add((int)(C01(f.Throttle) * 100));
            h.Add((int)(C01(f.Brake) * 100));
            h.Add((int)(C01(f.Clutch) * 100));
            h.Add((int)(f.Steer * 100));
            h.Add(f.Sector);
            h.Add(f.Lap);
            h.Add(dr2);
            for (int i = 0; i < 4; i++) h.Add((int)(f.TyrePress[i] * 10));
            h.Add((int)(f.GLat * 100));
            h.Add((int)(f.GLon * 100));
            h.Add((int)(f.Fuel * 10));
            h.Add((int)(f.MaxFuel * 10));
            int ek = h.ToHashCode();
            if (ek != lastExtra)
            {
                lastExtra = ek;
                ExtraT.Text = BuildExtra(f);
            }
        }
    }
}
