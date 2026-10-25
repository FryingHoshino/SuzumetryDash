using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Suzumetry.Core;
using Suzumetry.Net;

namespace Suzumetry;

public partial class MainWindow : Window
{
    readonly Settings st;
    readonly Hub hub;
    readonly WebServer web;
    readonly HudWindow hud;
    readonly PadModel pad;
    PadSettingsWindow? padWin;
    HudSettingsWindow? hudWin;
    PresetWindow? presetWin;
    Game autoGame = Game.None;
    readonly DispatcherTimer ui;
    readonly DispatcherTimer saveTimer;
    readonly TextBlock[] tyreBlocks;
    bool ready;
    bool waiting;
    int authorState;
    DispatcherTimer? authorTimer;
    long lastSeq = -1;
    HwndSource? src;

    public MainWindow()
    {
        InitializeComponent();
        st = Settings.Load();
        hub = new Hub(st);
        web = new WebServer(hub);
        pad = new PadModel(st);
        hud = new HudWindow(hub, st, pad);
        tyreBlocks = new[] { Tyre0, Tyre1, Tyre2, Tyre3 };

        ReadSlider.Value = st.ReadHz;
        StreamSlider.Value = st.StreamHz;
        GameCombo.SelectedIndex = st.ForceGame;
        PortBox.Text = st.Port.ToString();
        Dr2PortBox.Text = st.Dr2Port.ToString();
        Dr2FwdBox.Text = st.Dr2ForwardPort.ToString();
        HudCheck.IsChecked = st.HudEnabled;
        HudLockCheck.IsChecked = false;
        ScaleSlider.Value = st.HudScale;
        OpacitySlider.Value = st.HudOpacity;
        ChkPedals.IsChecked = st.HudPedals;
        ChkTyres.IsChecked = st.HudTyres;
        ChkLaps.IsChecked = st.HudLaps;
        ChkFuel.IsChecked = st.HudFuel;
        ChkPos.IsChecked = st.HudPos;
        ChkHeader.IsChecked = st.HudHeader;
        ChkProg.IsChecked = st.HudProgress;
        ChkG.IsChecked = st.HudGForce;
        ChkSteer.IsChecked = st.HudSteer;
        ChkWeather.IsChecked = st.HudWeather;
        ChkMph.IsChecked = st.HudMph;
        ReadLabel.Text = "Read rate: " + st.ReadHz + " Hz";
        StreamLabel.Text = "Stream rate: " + st.StreamHz + " Hz";

        ReadSlider.ValueChanged += (_, e) =>
        {
            if (!ready) return;
            st.ReadHz = (int)e.NewValue;
            ReadLabel.Text = "Read rate: " + st.ReadHz + " Hz";
            Touch();
        };
        StreamSlider.ValueChanged += (_, e) =>
        {
            if (!ready) return;
            st.StreamHz = (int)e.NewValue;
            StreamLabel.Text = "Stream rate: " + st.StreamHz + " Hz";
            Touch();
        };
        GameCombo.SelectionChanged += (_, _) =>
        {
            if (!ready || GameCombo.SelectedIndex < 0) return;
            st.ForceGame = GameCombo.SelectedIndex;
            Touch();
        };
        ScaleSlider.ValueChanged += (_, _) => ApplyHud();
        OpacitySlider.ValueChanged += (_, _) => ApplyHud();
        foreach (var c in new[] { HudCheck, HudLockCheck, ChkPedals, ChkTyres, ChkLaps, ChkFuel, ChkPos, ChkHeader, ChkProg, ChkG, ChkSteer, ChkWeather, ChkMph })
        {
            c.Checked += (_, _) => ApplyHud();
            c.Unchecked += (_, _) => ApplyHud();
        }
        PortBtn.Click += (_, _) =>
        {
            if (!int.TryParse(PortBox.Text, out var p) || p < 1024 || p > 65535)
            {
                PortBox.Text = st.Port.ToString();
                return;
            }
            st.Port = p;
            web.Start(p);
            RefreshUrls();
            Touch();
        };
        Dr2PortBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(Dr2PortBox.Text, out var p) && p >= 1024 && p <= 65535)
            {
                st.Dr2Port = p;
                Touch();
            }
            else Dr2PortBox.Text = st.Dr2Port.ToString();
        };
        Dr2FwdBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(Dr2FwdBox.Text, out var p) && p >= 0 && p <= 65535)
            {
                st.Dr2ForwardPort = p;
                Touch();
            }
            else Dr2FwdBox.Text = st.Dr2ForwardPort.ToString();
        };
        Dr2Btn.Click += (_, _) =>
        {
            if (int.TryParse(Dr2PortBox.Text, out var p) && p >= 1024 && p <= 65535) st.Dr2Port = p;
            MessageBox.Show(this, Dr2Config.Patch(st.Dr2Port), "Suzumetry");
        };
        FwBtn.Click += (_, _) => OpenFirewall();
        LoadAuthor();
        AuthorLink.MouseLeftButtonUp += (_, _) => ShowAuthor();
        ContentRendered += (_, _) => ShowAuthor();
        AuthorLayer.MouseLeftButtonDown += (_, _) => HideAuthor();
        UrlList.SelectionChanged += (_, _) =>
        {
            if (UrlList.SelectedItem is string u)
            {
                try { Clipboard.SetText(u); } catch { }
            }
        };

        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            st.Save();
        };
        ui = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
        ui.Tick += Tick;

        hub.Start();
        web.Start(st.Port);
        RefreshUrls();
        hud.ApplySettings();
        if (st.HudEnabled) hud.Show();
        ui.Start();
        AddPadControls();
        ready = true;
    }

    void AddPadControls()
    {
        if (ChkMph.Parent is not Panel host) return;
        int i = host.Children.IndexOf(ChkMph) + 1;
        var padCheck = new CheckBox { Content = "Mouse pad (raw input)", IsChecked = st.HudPad, Margin = new Thickness(0, 10, 0, 0) };
        RoutedEventHandler pc = (_, _) =>
        {
            st.HudPad = padCheck.IsChecked == true;
            hud.ApplySettings();
            Touch();
        };
        padCheck.Checked += pc;
        padCheck.Unchecked += pc;
        var fpsLabel = new TextBlock { Style = (Style)FindResource("Dim"), Margin = new Thickness(0, 10, 0, 0), Text = "HUD refresh: " + st.HudFps + " FPS" };
        var fps = new Slider { Minimum = 10, Maximum = 240, TickFrequency = 5, IsSnapToTickEnabled = true, Value = st.HudFps };
        fps.ValueChanged += (_, e) =>
        {
            st.HudFps = (int)e.NewValue;
            fpsLabel.Text = "HUD refresh: " + st.HudFps + " FPS";
            hud.ApplySettings();
            Touch();
        };
        var open = new Button { Content = "Mouse pad settings...", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        open.Click += (_, _) => OpenPadSettings();
        host.Children.Insert(i, padCheck);
        host.Children.Insert(i + 1, fpsLabel);
        host.Children.Insert(i + 2, fps);
        host.Children.Insert(i + 3, open);
        var hudStyle = new Button { Content = "Main HUD style and data...", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        hudStyle.Click += (_, _) => OpenHudSettings();
        host.Children.Insert(i, hudStyle);
        var presetBtn = new Button { Content = "Presets...", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        presetBtn.Click += (_, _) => OpenPresets();
        host.Children.Insert(i + 1, presetBtn);
    }

    void OpenPresets()
    {
        if (presetWin != null)
        {
            presetWin.Activate();
            return;
        }
        presetWin = new PresetWindow(st, PresetApplied, Touch) { Owner = this };
        presetWin.Closed += (_, _) => presetWin = null;
        presetWin.Show();
    }

    void PresetApplied()
    {
        hud.ApplySettings();
        padWin?.Reload();
        hudWin?.Reload();
        Touch();
    }

    void AutoPreset()
    {
        if (!st.PresetAuto)
        {
            autoGame = Game.None;
            return;
        }
        var g = hub.Game;
        if (g == autoGame) return;
        autoGame = g;
        if (g == Game.None) return;
        if (st.PresetMap.TryGetValue(g.ToString(), out var name) && Presets.Load(st, name)) PresetApplied();
    }

    void OpenHudSettings()
    {
        if (hudWin != null)
        {
            hudWin.Activate();
            return;
        }
        hud.SetPreview(true);
        hudWin = new HudSettingsWindow(st, () =>
        {
            hud.ApplySettings();
            Touch();
        }, m => hud.PreviewMode = m) { Owner = this };
        hudWin.Closed += (_, _) =>
        {
            hudWin = null;
            hud.SetPreview(false);
        };
        hudWin.Show();
    }

    void OpenPadSettings()
    {
        if (padWin != null)
        {
            padWin.Activate();
            return;
        }
        padWin = new PadSettingsWindow(st, pad, () =>
        {
            hud.ApplySettings();
            Touch();
        }, () => hub.Active ? hub.Current : null) { Owner = this };
        padWin.Closed += (_, _) => padWin = null;
        padWin.Show();
    }

    void Touch()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    void RefreshUrls() => UrlList.ItemsSource = WebServer.Urls(st.Port);

    void ApplyHud()
    {
        if (!ready) return;
        st.HudEnabled = HudCheck.IsChecked == true;
        st.HudScale = ScaleSlider.Value;
        st.HudOpacity = OpacitySlider.Value;
        st.HudPedals = ChkPedals.IsChecked == true;
        st.HudTyres = ChkTyres.IsChecked == true;
        st.HudLaps = ChkLaps.IsChecked == true;
        st.HudFuel = ChkFuel.IsChecked == true;
        st.HudPos = ChkPos.IsChecked == true;
        st.HudHeader = ChkHeader.IsChecked == true;
        st.HudProgress = ChkProg.IsChecked == true;
        st.HudGForce = ChkG.IsChecked == true;
        st.HudSteer = ChkSteer.IsChecked == true;
        st.HudWeather = ChkWeather.IsChecked == true;
        st.HudMph = ChkMph.IsChecked == true;
        hud.ApplySettings();
        hud.SetLocked(HudLockCheck.IsChecked != true);
        if (st.HudEnabled)
        {
            if (!hud.IsVisible) hud.Show();
        }
        else if (hud.IsVisible) hud.Hide();
        Touch();
    }

    void OpenFirewall()
    {
        try
        {
            var args = $"/c netsh advfirewall firewall delete rule name=\"Suzumetry\" >nul & netsh advfirewall firewall add rule name=\"Suzumetry\" dir=in action=allow protocol=TCP localport={st.Port}";
            Process.Start(new ProcessStartInfo("cmd.exe", args)
            {
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception e)
        {
            Log.Write(e);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        src = HwndSource.FromHwnd(h);
        src?.AddHook(Hook);
        Native.RegisterHotKey(h, 1, 0x4003, 0x48);
        Native.RegisterHotKey(h, 2, 0x4003, 0x4C);
        Native.RegisterHotKey(h, 3, 0x4003, 0x52);
    }

    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x0312)
        {
            int id = w.ToInt32();
            if (id == 1) HudCheck.IsChecked = HudCheck.IsChecked != true;
            else if (id == 2) HudLockCheck.IsChecked = HudLockCheck.IsChecked != true;
            else if (id == 3) pad.Reset();
            handled = true;
        }
        return IntPtr.Zero;
    }

    static string T(float t, float p) => (t > 0 ? t.ToString("0") + "°" : "--") + "  " + (p > 0 ? p.ToString("0.0") : "--");

    void LoadAuthor()
    {
        AuthorName.Text = Author.Name;
        AuthorInitial.Text = Author.Initial;
        authorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        authorTimer.Tick += (_, _) => HideAuthor();
        var data = Author.Image;
        if (data == null) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(data);
            bmp.EndInit();
            bmp.Freeze();
            AuthorImg.Fill = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
            AuthorInitial.Text = "";
        }
        catch (Exception e)
        {
            Log.Write(e);
        }
    }

    void ShowAuthor()
    {
        if (authorState != 0) return;
        authorState = 1;
        AuthorLayer.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var d = TimeSpan.FromMilliseconds(380);
        AuthorShade.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
        AuthorCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, d) { EasingFunction = ease });
        CardMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(30, 0, d) { EasingFunction = ease });
        authorTimer?.Stop();
        authorTimer?.Start();
    }

    void HideAuthor()
    {
        if (authorState != 1) return;
        authorState = 2;
        authorTimer?.Stop();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var d = TimeSpan.FromMilliseconds(220);
        AuthorShade.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, d));
        AuthorCard.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, d) { EasingFunction = ease });
        var up = new DoubleAnimation(0, -44, d) { EasingFunction = ease };
        up.Completed += (_, _) =>
        {
            AuthorLayer.Visibility = Visibility.Collapsed;
            authorState = 0;
        };
        CardMove.BeginAnimation(TranslateTransform.YProperty, up);
    }

    void SetWaiting(bool v)
    {
        if (v == waiting) return;
        waiting = v;
        Spinner.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
        if (v)
            SpinRot.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
        else
            SpinRot.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    void Tick(object? s, EventArgs e)
    {
        AutoPreset();
        ServerText.Text = web.Error != null ? "Server error: " + web.Error : web.Running ? "Server online on port " + st.Port : "Server offline";
        ClientsText.Text = "Connected devices: " + web.Clients;
        var f = hub.Current;
        if (f == null || !hub.Active)
        {
            GameText.Text = hub.Game == Game.None ? "Waiting for game"
                : hub.Game == Game.DR2 && Dr2Reader.LastError != null ? "DR2 · " + Dr2Reader.LastError
                : hub.Game + " · no data";
            SetWaiting(true);
            return;
        }
        SetWaiting(false);
        GameText.Text = f.Game.ToString();
        if (f.Seq == lastSeq) return;
        lastSeq = f.Seq;
        CarText.Text = f.Car;
        TrackText.Text = f.Track;
        GearText.Text = f.Gear < 0 ? "R" : f.Gear == 0 ? "N" : f.Gear.ToString();
        SpeedText.Text = ((int)f.Speed).ToString();
        RpmText.Text = f.Rpm + " / " + f.MaxRpm + " rpm";
        RpmBar.Value = f.MaxRpm > 0 ? Math.Clamp((double)f.Rpm / f.MaxRpm, 0, 1) : 0;
        ThrBar.Value = Math.Clamp(f.Throttle, 0f, 1f);
        BrkBar.Value = Math.Clamp(f.Brake, 0f, 1f);
        CltBar.Value = Math.Clamp(f.Clutch, 0f, 1f);
        CurText.Text = Fmt.Time(f.CurMs);
        LastText.Text = Fmt.Time(f.LastMs);
        BestText.Text = Fmt.Time(f.BestMs);
        InfoText.Text = $"Lap {f.Lap + 1}   P{f.Position}   Fuel {f.Fuel:0.0} L   G {f.GLat:0.00} / {f.GLon:0.00}";
        for (int i = 0; i < 4; i++) tyreBlocks[i].Text = T(f.TyreTemp[i], f.TyrePress[i]);
    }

    protected override void OnClosed(EventArgs e)
    {
        ui.Stop();
        saveTimer.Stop();
        var h = new WindowInteropHelper(this).Handle;
        Native.UnregisterHotKey(h, 1);
        Native.UnregisterHotKey(h, 2);
        Native.UnregisterHotKey(h, 3);
        padWin?.Close();
        hudWin?.Close();
        presetWin?.Close();
        st.Save();
        hud.Shutdown();
        pad.Dispose();
        web.Dispose();
        hub.Dispose();
        base.OnClosed(e);
    }
}
