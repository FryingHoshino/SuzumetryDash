using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Suzumetry.Core;

namespace Suzumetry;

public sealed class PresetWindow : Window
{
    static readonly Brush Fg = Frozen(0xE8, 0xEA, 0xF0);
    static readonly Brush Dim = Frozen(0x8A, 0x90, 0xA2);
    static readonly Brush Acc = Frozen(0xFF, 0x5A, 0x7A);
    static readonly Brush PanelBg = Frozen(0x17, 0x19, 0x20);
    static readonly Brush Field = Frozen(0x0E, 0x0F, 0x13);
    static readonly Brush Edge = Frozen(0x2A, 0x2F, 0x3A);

    readonly Settings st;
    readonly Action applied;
    readonly Action saved;
    readonly StackPanel panel = new();
    readonly TextBlock status = new() { Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    string selected = "";

    static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public PresetWindow(Settings st, Action applied, Action saved)
    {
        this.st = st;
        this.applied = applied;
        this.saved = saved;
        Title = "Suzumetry - Presets";
        Width = 460;
        Height = 720;
        MinWidth = 380;
        MinHeight = 420;
        Background = Frozen(0x0E, 0x0F, 0x13);
        Foreground = Fg;
        FontFamily = new FontFamily("Segoe UI");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16) };
        sv.Content = new Border { Background = PanelBg, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = panel };
        Content = sv;
        Build();
    }

    public void Refresh() => Build();

    void Say(string t) => status.Text = t;

    Button Btn(string text, Action click)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 5, 12, 5) };
        b.Click += (_, _) => click();
        return b;
    }

    void Head(string t) =>
        panel.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Foreground = Acc, Margin = new Thickness(0, 16, 0, 4) });

    void DoLoad()
    {
        if (selected.Length == 0)
        {
            Say("Select a preset first.");
            return;
        }
        if (Presets.Load(st, selected))
        {
            applied();
            Say("Loaded \"" + selected + "\".");
        }
        else Say("Could not load that preset.");
    }

    void DoDelete()
    {
        if (selected.Length == 0)
        {
            Say("Select a preset first.");
            return;
        }
        string gone = selected;
        if (Presets.Delete(gone))
        {
            selected = "";
            Build();
            Say("Deleted \"" + gone + "\".");
        }
        else Say("Could not delete that preset.");
    }

    void DoSave(string name)
    {
        string clean = Presets.Clean(name);
        if (clean.Length == 0)
        {
            Say("Enter a name first.");
            return;
        }
        if (Presets.Save(st, clean))
        {
            selected = clean;
            Build();
            Say("Saved \"" + clean + "\".");
        }
        else Say("Could not save the preset.");
    }

    void DoExport()
    {
        if (selected.Length == 0)
        {
            Say("Select a preset first.");
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Suzumetry preset (*.json)|*.json", FileName = selected };
        if (dlg.ShowDialog(this) != true) return;
        Say(Presets.Export(selected, dlg.FileName) ? "Exported to " + dlg.FileName : "Could not export the preset.");
    }

    void DoImport()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Suzumetry preset (*.json)|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        var name = Presets.Import(dlg.FileName);
        if (name == null)
        {
            Say("That file is not a valid preset.");
            return;
        }
        selected = name;
        Build();
        Say("Imported as \"" + name + "\".");
    }

    void Build()
    {
        panel.Children.Clear();
        panel.Children.Add(new TextBlock { Text = "Presets", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Fg });
        panel.Children.Add(new TextBlock { Text = "A preset stores the main HUD style, shift lights, warnings, extra data and every mouse pad setting.", Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });

        var names = Presets.List();
        var nameBox = new TextBox
        {
            Text = selected,
            Background = Field,
            Foreground = Fg,
            CaretBrush = Fg,
            BorderBrush = Edge,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 4, 0, 0)
        };

        Head("Saved presets");
        var list = new ListBox { Height = 150, Background = Field, Foreground = Fg, BorderBrush = Edge, ItemsSource = names };
        if (names.Contains(selected)) list.SelectedItem = selected;
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is string n)
            {
                selected = n;
                nameBox.Text = n;
            }
        };
        list.MouseDoubleClick += (_, _) => DoLoad();
        panel.Children.Add(list);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(Btn("Load", DoLoad));
        row.Children.Add(Btn("Delete", DoDelete));
        row.Children.Add(Btn("Export...", DoExport));
        row.Children.Add(Btn("Import...", DoImport));
        panel.Children.Add(row);

        Head("Save current style");
        panel.Children.Add(nameBox);
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        saveRow.Children.Add(Btn("Save preset", () => DoSave(nameBox.Text)));
        panel.Children.Add(saveRow);
        panel.Children.Add(new TextBlock { Text = "Saving under an existing name overwrites it.", Foreground = Dim, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });

        Head("Automatic switching");
        var auto = new CheckBox { Content = "Switch preset automatically when a game starts", IsChecked = st.PresetAuto, Foreground = Fg };
        RoutedEventHandler h = (_, _) =>
        {
            st.PresetAuto = auto.IsChecked == true;
            saved();
        };
        auto.Checked += h;
        auto.Unchecked += h;
        panel.Children.Add(auto);
        foreach (var g in Enum.GetNames(typeof(Game)).Where(n => n != "None"))
        {
            panel.Children.Add(new TextBlock { Text = g, Foreground = Dim, Margin = new Thickness(0, 8, 0, 4) });
            var c = new ComboBox();
            c.Items.Add("(none)");
            foreach (var n in names) c.Items.Add(n);
            st.PresetMap.TryGetValue(g, out var cur);
            c.SelectedItem = cur != null && names.Contains(cur) ? cur : "(none)";
            string key = g;
            c.SelectionChanged += (_, _) =>
            {
                if (c.SelectedItem is not string s) return;
                if (s == "(none)") st.PresetMap.Remove(key);
                else st.PresetMap[key] = s;
                saved();
            };
            panel.Children.Add(c);
        }

        panel.Children.Add(status);
    }
}
