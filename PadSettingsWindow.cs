using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Suzumetry.Core;

namespace Suzumetry;

public sealed class PadSettingsWindow : Window
{
    static readonly Brush Fg = Frozen(0xE8, 0xEA, 0xF0);
    static readonly Brush Dim = Frozen(0x8A, 0x90, 0xA2);
    static readonly Brush Acc = Frozen(0xFF, 0x5A, 0x7A);
    static readonly Brush PanelBg = Frozen(0x17, 0x19, 0x20);
    static readonly Brush Field = Frozen(0x0E, 0x0F, 0x13);
    static readonly Brush Edge = Frozen(0x2A, 0x2F, 0x3A);
    static readonly Brush Bad = Frozen(0xFF, 0x5A, 0x5A);
    static readonly Type SType = typeof(Settings);

    readonly Settings st;
    readonly PadModel pad;
    readonly Action changed;
    readonly Func<Snap?> live;
    readonly CoordView view = new();
    readonly Border card = new();
    readonly TextBlock cap = new() { Text = "MOUSE" };
    readonly TextBlock capDev = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock devList = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock info = new();
    readonly TextBlock infoX = new();
    readonly StackPanel panel = new();
    readonly DispatcherTimer timer;

    static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public PadSettingsWindow(Settings st, PadModel pad, Action changed, Func<Snap?> live)
    {
        this.st = st;
        this.pad = pad;
        this.changed = changed;
        this.live = live;
        Title = "Suzumetry - Mouse pad";
        Width = 800;
        Height = 740;
        MinWidth = 700;
        MinHeight = 520;
        Background = Frozen(0x0E, 0x0F, 0x13);
        Foreground = Fg;
        FontFamily = new FontFamily("Segoe UI");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        view.Bind(st, pad);
        var mono = new FontFamily("Consolas");
        cap.TextTrimming = TextTrimming.CharacterEllipsis;
        capDev.Margin = new Thickness(0, 1, 0, 4);
        info.FontFamily = mono;
        info.Margin = new Thickness(0, 6, 0, 0);
        infoX.FontFamily = mono;
        infoX.Margin = new Thickness(0, 2, 0, 0);

        var cs = new StackPanel();
        cs.Children.Add(cap);
        cs.Children.Add(capDev);
        cs.Children.Add(view);
        cs.Children.Add(info);
        cs.Children.Add(infoX);
        card.CornerRadius = new CornerRadius(12);
        card.Padding = new Thickness(10);
        card.HorizontalAlignment = HorizontalAlignment.Left;
        card.Child = cs;

        var reset = new Button { Content = "Reset center", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
        reset.Click += (_, _) => pad.Reset();
        var defaults = new Button { Content = "Restore defaults", Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
        defaults.Click += (_, _) =>
        {
            st.ResetPad();
            changed();
            Build();
        };

        var left = new Border { Background = PanelBg, CornerRadius = new CornerRadius(12), Padding = new Thickness(16), VerticalAlignment = VerticalAlignment.Top };
        var lp = new StackPanel();
        lp.Children.Add(new TextBlock { Text = "Live preview", Foreground = Dim, Margin = new Thickness(0, 0, 0, 8) });
        lp.Children.Add(card);
        lp.Children.Add(reset);
        lp.Children.Add(defaults);
        lp.Children.Add(new TextBlock { Text = "Ctrl+Alt+R also recenters", Foreground = Dim, FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        left.Child = lp;
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        var right = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var rb = new Border { Background = PanelBg, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = panel };
        right.Content = rb;
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        Content = root;

        Build();
        pad.Acquire();
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var f = live();
            view.Live = f;
            view.Refresh();
            var ps = pad.State();
            InputDevices.Caption(ps, st, out string ch, out string cd);
            if (cap.Text != ch) cap.Text = ch;
            if (capDev.Text != cd)
            {
                capDev.Text = cd;
                capDev.Visibility = cd.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            info.Text = PadText.Data(ps, f, st);
            string x = PadText.Extra(ps, st);
            infoX.Text = x;
            infoX.Visibility = x.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        timer.Start();
        Closed += (_, _) =>
        {
            timer.Stop();
            pad.Release();
        };
    }

    void Scan()
    {
        devList.Text = "Scanning...";
        Task.Run(() =>
        {
            InputDevices.Clear();
            var list = InputDevices.ListAll();
            var sb = new System.Text.StringBuilder();
            foreach (var d in list)
            {
                sb.Append(d.Kind).Append(": ").Append(d.Name).Append('\n');
                var detail = string.Join(" · ", new[] { d.Id, d.Bus, d.Port }.Where(x => x.Length > 0));
                if (detail.Length > 0) sb.Append("    ").Append(detail).Append('\n');
            }
            if (list.Count == 0) sb.Append("No devices found.");
            Dispatcher.BeginInvoke(() => devList.Text = sb.ToString().TrimEnd('\n'));
        });
    }

    public void Reload() => Build();

    void Changed()
    {
        st.Clamp();
        ApplyTextStyle();
        changed();
    }

    static void TextStyle(TextBlock t, string color, double opacity, double size)
    {
        t.Foreground = PadStyle.MakeBrush(color, opacity);
        t.FontSize = size;
    }

    void ApplyTextStyle()
    {
        card.Background = PadStyle.MakeBrush(st.PadCardColor, st.PadCardOpacity);
        TextStyle(cap, st.PadCapColor, st.PadCapOpacity, st.PadCapSize);
        TextStyle(capDev, st.PadCapColor, st.PadCapOpacity, Math.Max(6, st.PadCapSize - 1));
        cap.MaxWidth = st.PadSize;
        capDev.MaxWidth = st.PadSize;
        TextStyle(info, st.PadTextColor, st.PadTextOpacity, st.PadTextSize);
        TextStyle(infoX, st.PadExtraColor, st.PadExtraOpacity, st.PadExtraSize);
    }

    string GetS(string name) => (string)SType.GetProperty(name)!.GetValue(st)!;
    double GetD(string name) => (double)SType.GetProperty(name)!.GetValue(st)!;
    void SetP(string name, object value) => SType.GetProperty(name)!.SetValue(st, value);

    void Head(string t) =>
        panel.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Foreground = Acc, Margin = new Thickness(0, 16, 0, 4) });

    void Check(string label, Func<bool> get, Action<bool> set)
    {
        var c = new CheckBox { Content = label, IsChecked = get(), Foreground = Fg, Margin = new Thickness(0, 3, 0, 0) };
        RoutedEventHandler h = (_, _) =>
        {
            set(c.IsChecked == true);
            Changed();
        };
        c.Checked += h;
        c.Unchecked += h;
        panel.Children.Add(c);
    }

    void Slide(string label, double min, double max, double step, Func<double> get, Action<double> set, string fmt, Panel? host = null)
    {
        var t = new TextBlock { Foreground = Dim, Margin = new Thickness(0, 8, 0, 0) };
        var s = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = get() };
        void Upd() => t.Text = label + ": " + s.Value.ToString(fmt);
        Upd();
        s.ValueChanged += (_, e) =>
        {
            set(e.NewValue);
            Upd();
            Changed();
        };
        var h = host ?? panel;
        h.Children.Add(t);
        h.Children.Add(s);
    }

    void Combo(string label, string[] items, Func<int> get, Action<int> set)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = Dim, Margin = new Thickness(0, 8, 0, 4) });
        var c = new ComboBox();
        foreach (var i in items) c.Items.Add(i);
        c.SelectedIndex = Math.Clamp(get(), 0, items.Length - 1);
        c.SelectionChanged += (_, _) =>
        {
            if (c.SelectedIndex < 0) return;
            set(c.SelectedIndex);
            Changed();
        };
        panel.Children.Add(c);
    }

    void ColorEditor(Panel host, string prop)
    {
        var box = new TextBox
        {
            Text = GetS(prop),
            Width = 90,
            MaxLength = 7,
            Background = Field,
            Foreground = Fg,
            CaretBrush = Fg,
            BorderBrush = Edge,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var sw = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(5),
            BorderBrush = Dim,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0),
            Background = PadStyle.MakeBrush(GetS(prop), 1)
        };
        box.TextChanged += (_, _) =>
        {
            if (PadStyle.TryParse(box.Text, out var c))
            {
                box.Foreground = Fg;
                sw.Background = PadStyle.MakeBrush(PadStyle.Hex(c), 1);
                SetP(prop, PadStyle.Hex(c));
                Changed();
            }
            else box.Foreground = Bad;
        };
        var wrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 4) };
        foreach (var hex in PadStyle.Palette)
        {
            var b = new Border
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 4),
                CornerRadius = new CornerRadius(4),
                BorderBrush = Edge,
                BorderThickness = new Thickness(1),
                Background = PadStyle.MakeBrush(hex, 1),
                Cursor = Cursors.Hand,
                ToolTip = hex
            };
            b.MouseLeftButtonUp += (_, _) => box.Text = hex;
            wrap.Children.Add(b);
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(sw);
        row.Children.Add(box);
        host.Children.Add(wrap);
        host.Children.Add(row);
    }

    void Element(string title, string key, string? sliderLabel = null, string? sliderProp = null, double min = 0, double max = 0, double step = 0.1, string fmt = "0.0")
    {
        var sp = new StackPanel { Margin = new Thickness(8, 4, 0, 8) };
        ColorEditor(sp, key + "Color");
        Slide("Opacity", 0, 1, 0.05, () => GetD(key + "Opacity"), v => SetP(key + "Opacity", Math.Round(v, 2)), "0.00", sp);
        if (sliderProp != null)
            Slide(sliderLabel!, min, max, step, () => GetD(sliderProp), v => SetP(sliderProp, Math.Round(v, 2)), fmt, sp);
        panel.Children.Add(new Expander
        {
            Header = new TextBlock { Text = title, Foreground = Fg },
            Content = sp,
            Foreground = Fg,
            Margin = new Thickness(0, 4, 0, 0)
        });
    }

    void Build()
    {
        panel.Children.Clear();
        Head("Placement");
        Check("Show mouse pad on HUD", () => st.HudPad, v => st.HudPad = v);
        Check("Show even when no game is running", () => st.PadAlways, v => st.PadAlways = v);
        Combo("Side of the main HUD", new[] { "Left", "Right", "Top", "Bottom" }, () => st.PadSide, v => st.PadSide = v);
        Slide("Pad size (px)", 120, 360, 10, () => st.PadSize, v => st.PadSize = (int)v, "0");

        Head("Input");
        Slide("Full-scale range (counts)", 100, 3000, 50, () => st.PadRange, v => st.PadRange = (int)v, "0");
        Slide("Sensitivity", 0.1, 5, 0.1, () => st.PadSens, v => st.PadSens = v, "0.0");
        Check("Invert X", () => st.PadInvertX, v => st.PadInvertX = v);
        Check("Invert Y", () => st.PadInvertY, v => st.PadInvertY = v);
        Slide("Auto-center strength (0 = off)", 0, 20, 1, () => st.PadCenter, v => st.PadCenter = (int)v, "0");
        Check("Auto-center Y too", () => st.PadCenterY, v => st.PadCenterY = v);
        Check("Middle click recenters", () => st.PadMidReset, v => st.PadMidReset = v);
        Slide("Input sampling rate (Hz)", 60, 1000, 20, () => st.PadHz, v => st.PadHz = (int)v, "0");

        Head("Display");
        Slide("Trail length (ms, 0 = off)", 0, 4000, 100, () => st.PadTrailMs, v => st.PadTrailMs = (int)v, "0");
        Slide("Dot size", 2, 16, 1, () => st.PadDot, v => st.PadDot = (int)v, "0");
        Slide("Grid divisions", 2, 20, 2, () => st.PadDivs, v => st.PadDivs = (int)v, "0");
        Check("Grid lines", () => st.PadGrid, v => st.PadGrid = v);
        Check("Axis numbers", () => st.PadLabels, v => st.PadLabels = v);
        Check("Crosshair lines to axes", () => st.PadCross, v => st.PadCross = v);

        Head("Input source");
        Combo("Pad is driven by", new[] { "Mouse (raw input)", "Gamepad (XInput)", "Wheel or joystick (Windows game controller)" }, () => st.PadSource, v =>
        {
            st.PadSource = v;
            st.PadDevice = 0;
            st.PadAxisX = 0;
            st.PadAxisY = v == 0 ? 0 : 1;
            Dispatcher.BeginInvoke(new Action(Build));
        });
        if (st.PadSource != 0)
        {
            var axes = st.PadSource == 1 ? AxisInput.XInputAxes : AxisInput.JoyAxes;
            Combo("Device", AxisInput.ListDevices(st.PadSource), () => st.PadDevice, v => st.PadDevice = v);
            Combo("Axis for X", axes, () => st.PadAxisX, v => st.PadAxisX = v);
            Combo("Axis for Y", axes, () => st.PadAxisY, v => st.PadAxisY = v);
            Slide("Dead zone", 0, 0.4, 0.01, () => st.PadDeadzone, v => st.PadDeadzone = Math.Round(v, 2), "0.00");
            panel.Children.Add(new TextBlock { Text = "Axis sources show their absolute position. Reset center sets the current position as zero. Range, sensitivity and auto-center apply to the mouse only.", Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        }

        Head("Input device");
        Check("Show device type, name, ID and port on the caption", () => st.PadCapInfo, v => st.PadCapInfo = v);
        Combo("Caption detail", new[] { "Name only", "Name and ID", "Name, ID and USB port" }, () => st.PadCapDetail, v => st.PadCapDetail = v);
        panel.Children.Add(new TextBlock { Text = "The caption shows the device driving the pad. The scan lists every mouse, gamepad and wheel Windows can see.", Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        var scan = new Button { Content = "Scan devices", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12, 4, 12, 4) };
        scan.Click += (_, _) => Scan();
        panel.Children.Add(scan);
        devList.Foreground = Dim;
        devList.FontSize = 12;
        devList.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(devList);

        Head("Data on HUD");
        Check("Raw values (counts) instead of -1 to 1", () => st.PadRaw, v => st.PadRaw = v);
        Check("Extra data (poll rate, speed, buttons, wheel)", () => st.PadExtra, v => st.PadExtra = v);
        Check("Game pedals and steering text + steering marker", () => st.PadGame, v => st.PadGame = v);

        Head("Colors, opacity, thickness and fonts");
        panel.Children.Add(new TextBlock { Text = "Click an element to edit it. Pick a swatch or type a hex color.", Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        Element("HUD card background", "PadCard");
        Element("Caption (MOUSE)", "PadCap", "Font size", "PadCapSize", 6, 24, 1, "0");
        Element("Plot background", "PadBg");
        Element("Grid lines", "PadGrid", "Thickness", "PadGridThick", 0.5, 6, 0.1);
        Element("Axes and tick marks", "PadAxis", "Thickness", "PadAxisThick", 0.5, 6, 0.1);
        Element("Axis arrows", "PadArrow", "Arrow size", "PadArrowSize", 2, 14, 1, "0");
        Element("Axis numbers", "PadNum", "Font size", "PadNumSize", 6, 20, 1, "0");
        Element("Axis letters (x, y)", "PadLetter", "Font size", "PadLetterSize", 6, 24, 1, "0");
        Element("Crosshair lines", "PadCross", "Thickness", "PadCrossThick", 0.5, 6, 0.1);
        Element("Trail", "PadTrail", "Thickness", "PadTrailThick", 0.5, 10, 0.1);
        Element("Dot fill", "PadDot");
        Element("Dot ring", "PadRing", "Thickness", "PadRingThick", 0.5, 6, 0.1);
        Element("Steering marker", "PadSteer", "Thickness", "PadSteerThick", 0.5, 8, 0.1);
        Element("Data text", "PadText", "Font size", "PadTextSize", 8, 28, 1, "0");
        Element("Extra data text", "PadExtra", "Font size", "PadExtraSize", 8, 28, 1, "0");

        ApplyTextStyle();
    }
}
