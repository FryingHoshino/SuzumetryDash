using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Suzumetry.Core;

namespace Suzumetry;

public sealed class HudSettingsWindow : Window
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
    readonly Action changed;
    readonly Action<int> preview;
    readonly StackPanel panel = new();

    static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public HudSettingsWindow(Settings st, Action changed, Action<int> preview)
    {
        this.st = st;
        this.changed = changed;
        this.preview = preview;
        Title = "Suzumetry - Main HUD style";
        Width = 500;
        Height = 760;
        MinWidth = 420;
        MinHeight = 480;
        Background = Frozen(0x0E, 0x0F, 0x13);
        Foreground = Fg;
        FontFamily = new FontFamily("Segoe UI");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16) };
        sv.Content = new Border { Background = PanelBg, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = panel };
        Content = sv;
        Build();
    }

    public void Reload()
    {
        preview(0);
        Build();
    }

    void Changed()
    {
        st.Clamp();
        changed();
    }

    string GetS(string name) => (string)SType.GetProperty(name)!.GetValue(st)!;
    double GetD(string name) => (double)SType.GetProperty(name)!.GetValue(st)!;
    void SetP(string name, object value) => SType.GetProperty(name)!.SetValue(st, value);

    void Head(string t) =>
        panel.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Foreground = Acc, Margin = new Thickness(0, 16, 0, 4) });

    void Note(string t) =>
        panel.Children.Add(new TextBlock { Text = t, Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });

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

    void Prop(string label, string prop, double min, double max, double step, string fmt) =>
        Slide(label, min, max, step, () => GetD(prop), v => SetP(prop, Math.Round(v, 2)), fmt);

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

    void Element(string title, string key)
    {
        var sp = new StackPanel { Margin = new Thickness(8, 4, 0, 8) };
        ColorEditor(sp, key + "Color");
        Slide("Opacity", 0, 1, 0.05, () => GetD(key + "Opacity"), v => SetP(key + "Opacity", Math.Round(v, 2)), "0.00", sp);
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
        panel.Children.Add(new TextBlock { Text = "Main HUD style", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Fg });
        Note("The HUD must be enabled. While this window is open the HUD shows sample data.");

        Head("Preview data");
        var pc = new ComboBox { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var i in new[] { "Tyre and brake temperatures", "Tyre temperatures only", "Brake temperatures only", "No tyre or brake data" }) pc.Items.Add(i);
        pc.SelectedIndex = 0;
        pc.SelectionChanged += (_, _) =>
        {
            if (pc.SelectedIndex >= 0) preview(pc.SelectedIndex);
        };
        panel.Children.Add(pc);

        Head("Extra data");
        Check("Show extra data (pedals, steering, sector, lap, tyre pressure, G-force, tank)", () => st.HudExtra, v => st.HudExtra = v);
        Note("Values the game does not provide are shown as null.");
        Prop("Extra data font size", "HsExtraSize", 8, 24, 1, "0");

        Head("Shift lights");
        Check("Show shift lights above the speed readout", () => st.HudShift, v => st.HudShift = v);
        Prop("Light count", "HsShiftCount", 4, 24, 1, "0");
        Prop("Light height (px)", "HsShiftHeight", 4, 30, 1, "0");
        Prop("First light at RPM (fraction of max)", "HsShiftStart", 0.3, 0.95, 0.01, "0.00");
        Prop("Flash at RPM (fraction of max)", "HsShiftFlash", 0.8, 1, 0.01, "0.00");

        Head("Warnings");
        Check("Flash warnings (brake and tyre overheat, pit limiter, low fuel)", () => st.HudWarn, v => st.HudWarn = v);
        Prop("Brake overheat at (°C)", "HsBrakeWarn", 200, 1500, 10, "0");
        Prop("Tyre overheat at (°C)", "HsTyreWarn", 60, 250, 5, "0");
        Prop("Low fuel below (L)", "HsFuelWarn", 0, 40, 1, "0");

        Head("Tyre and brake tiles");
        Note("Tyre data only: the tile background changes with temperature. Brake data only: the tile stays fixed and the text color changes. Both: the brake tile comes first and the tyre tile sits beside it.");
        Prop("Tyre tile background opacity", "HsTyreBgOpacity", 0, 1, 0.05, "0.00");
        Prop("Tyre cold below (°C)", "HsTyreCold", 20, 120, 5, "0");
        Prop("Tyre hot above (°C)", "HsTyreHot", 60, 200, 5, "0");
        Prop("Brake cold below (°C)", "HsBrakeCold", 50, 500, 10, "0");
        Prop("Brake hot above (°C)", "HsBrakeHot", 200, 1000, 10, "0");

        panel.Children.Add(new TextBlock { Text = "Colors and opacity", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Fg, Margin = new Thickness(0, 22, 0, 0) });
        Note("Click an element to edit it. Pick a swatch or type a hex color.");
        Head("Window and header");
        Element("HUD background", "HsBg");
        Element("Header (car and track)", "HsHead");
        Head("Gear, speed and RPM");
        Element("Gear number", "HsGear");
        Element("Speed", "HsSpeed");
        Element("Speed unit", "HsUnit");
        Element("RPM text", "HsRpmText");
        Element("RPM bar background", "HsRpmTrack");
        Element("RPM bar", "HsRpm");
        Element("RPM bar at redline", "HsRedline");
        Head("Position and status");
        Element("Position / progress %", "HsPos");
        Element("Pit / limiter text", "HsState");
        Head("Steering and progress bars");
        Element("Steering bar background", "HsSteerTrack");
        Element("Steering center line", "HsSteerCenter");
        Element("Steering marker", "HsSteerMark");
        Element("Progress bar background", "HsProgTrack");
        Element("Progress bar", "HsProg");
        Head("Pedals");
        Element("Pedal background", "HsPedalTrack");
        Element("Clutch pedal", "HsClt");
        Element("Brake pedal", "HsBrk");
        Element("Throttle pedal", "HsThr");
        Head("Tyre and brake temperature colors");
        Element("Tyre / brake caption", "HsTempCap");
        Element("Temperature: cold", "HsCold");
        Element("Temperature: optimal", "HsGood");
        Element("Temperature: hot", "HsHot");
        Element("Temperature: no reading", "HsIdle");
        Element("Brake tile background", "HsBrakeBg");
        Element("Tyre tile text", "HsTyreText");
        Head("G-force");
        Element("G-force rings", "HsGRing");
        Element("G-force dot", "HsGDot");
        Head("Lap times and info");
        Element("Current lap time", "HsCur");
        Element("Last lap time", "HsLast");
        Element("Best lap time", "HsBest");
        Element("Fuel and weather line", "HsInfo");
        Head("Shift lights");
        Element("Shift light: off", "HsShiftOff");
        Element("Shift light: low RPM", "HsShiftLow");
        Element("Shift light: mid RPM", "HsShiftMid");
        Element("Shift light: high RPM and flash", "HsShiftHigh");
        Head("Extra data");
        Element("Extra data text", "HsExtraText");

        var defaults = new Button { Content = "Restore defaults", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
        defaults.Click += (_, _) =>
        {
            st.ResetHudStyle();
            preview(0);
            changed();
            Build();
        };
        panel.Children.Add(defaults);
    }
}
