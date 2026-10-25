using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Suzumetry.Core;

namespace Suzumetry;

public sealed class CoordView : FrameworkElement
{
    static readonly Typeface Face = new("Consolas");

    readonly Pen[] trail = new Pen[12];
    readonly PadModel.P[] scratch = new PadModel.P[1024];
    Settings? st;
    PadModel? model;
    DrawingGroup? back;
    int styleKey;
    bool haveStyle;

    Brush plotBrush = Brushes.Transparent;
    Brush arrowBrush = Brushes.Transparent;
    Brush numBrush = Brushes.Transparent;
    Brush letterBrush = Brushes.Transparent;
    Brush dotBrush = Brushes.Transparent;
    Pen gridPen = null!;
    Pen axisPen = null!;
    Pen crossPen = null!;
    Pen ringPen = null!;
    Pen markerPen = null!;

    public Snap? Live { get; set; }

    public void Bind(Settings settings, PadModel m)
    {
        st = settings;
        model = m;
        haveStyle = false;
    }

    public void Refresh()
    {
        if (st == null) return;
        if ((int)DesiredSize.Width != st.PadSize) InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int n = st?.PadSize ?? 190;
        return new Size(n, n);
    }

    static int Key(Settings s)
    {
        var h = new HashCode();
        h.Add(s.PadSize); h.Add(s.PadDivs); h.Add(s.PadGrid); h.Add(s.PadLabels); h.Add(s.PadRaw); h.Add(s.PadRange);
        h.Add(s.PadBgColor); h.Add(s.PadBgOpacity);
        h.Add(s.PadGridColor); h.Add(s.PadGridOpacity); h.Add(s.PadGridThick);
        h.Add(s.PadAxisColor); h.Add(s.PadAxisOpacity); h.Add(s.PadAxisThick);
        h.Add(s.PadArrowColor); h.Add(s.PadArrowOpacity); h.Add(s.PadArrowSize);
        h.Add(s.PadNumColor); h.Add(s.PadNumOpacity); h.Add(s.PadNumSize);
        h.Add(s.PadLetterColor); h.Add(s.PadLetterOpacity); h.Add(s.PadLetterSize);
        h.Add(s.PadCrossColor); h.Add(s.PadCrossOpacity); h.Add(s.PadCrossThick);
        h.Add(s.PadTrailColor); h.Add(s.PadTrailOpacity); h.Add(s.PadTrailThick);
        h.Add(s.PadDotColor); h.Add(s.PadDotOpacity);
        h.Add(s.PadRingColor); h.Add(s.PadRingOpacity); h.Add(s.PadRingThick);
        h.Add(s.PadSteerColor); h.Add(s.PadSteerOpacity); h.Add(s.PadSteerThick);
        return h.ToHashCode();
    }

    void Rebuild(int n)
    {
        var s = st!;
        plotBrush = PadStyle.MakeBrush(s.PadBgColor, s.PadBgOpacity);
        arrowBrush = PadStyle.MakeBrush(s.PadArrowColor, s.PadArrowOpacity);
        numBrush = PadStyle.MakeBrush(s.PadNumColor, s.PadNumOpacity);
        letterBrush = PadStyle.MakeBrush(s.PadLetterColor, s.PadLetterOpacity);
        dotBrush = PadStyle.MakeBrush(s.PadDotColor, s.PadDotOpacity);
        gridPen = PadStyle.MakePen(s.PadGridColor, s.PadGridOpacity, s.PadGridThick);
        axisPen = PadStyle.MakePen(s.PadAxisColor, s.PadAxisOpacity, s.PadAxisThick);
        crossPen = PadStyle.MakePen(s.PadCrossColor, s.PadCrossOpacity, s.PadCrossThick);
        ringPen = PadStyle.MakePen(s.PadRingColor, s.PadRingOpacity, s.PadRingThick);
        markerPen = PadStyle.MakePen(s.PadSteerColor, s.PadSteerOpacity, s.PadSteerThick);
        var tc = PadStyle.Parse(s.PadTrailColor, Colors.White);
        double to = Math.Clamp(s.PadTrailOpacity, 0, 1);
        for (int i = 0; i < trail.Length; i++)
        {
            byte a = (byte)Math.Round((25 + i * 20) * to);
            var br = new SolidColorBrush(Color.FromArgb(a, tc.R, tc.G, tc.B));
            br.Freeze();
            var p = new Pen(br, s.PadTrailThick) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            trail[i] = p;
        }
        back = BuildBack(n);
    }

    void Arrow(DrawingContext dc, double x, double y, double dx, double dy)
    {
        double a = st!.PadArrowSize;
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            c.BeginFigure(new Point(x + dx * a, y + dy * a), true, true);
            c.LineTo(new Point(x - dx * a - dy * a, y - dy * a + dx * a), true, false);
            c.LineTo(new Point(x - dx * a + dy * a, y - dy * a - dx * a), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(arrowBrush, null, geo);
    }

    static FormattedText Text(string s, double size, double dpi, Brush brush) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, brush, dpi);

    DrawingGroup BuildBack(int n)
    {
        var s = st!;
        var g = new DrawingGroup();
        using (var dc = g.Open())
        {
            double c = n / 2.0, h = c - 14;
            int half = Math.Max(1, s.PadDivs / 2);
            double step = h / half;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            dc.DrawRoundedRectangle(plotBrush, null, new Rect(0, 0, n, n), 10, 10);
            if (s.PadGrid)
            {
                for (int k = -half; k <= half; k++)
                {
                    if (k == 0) continue;
                    double p = c + k * step;
                    dc.DrawLine(gridPen, new Point(p, c - h), new Point(p, c + h));
                    dc.DrawLine(gridPen, new Point(c - h, p), new Point(c + h, p));
                }
                dc.DrawRectangle(null, gridPen, new Rect(c - h, c - h, h * 2, h * 2));
            }
            double e = h + 8;
            dc.DrawLine(axisPen, new Point(c - e, c), new Point(c + e, c));
            dc.DrawLine(axisPen, new Point(c, c - e), new Point(c, c + e));
            Arrow(dc, c + e, c, 1, 0);
            Arrow(dc, c - e, c, -1, 0);
            Arrow(dc, c, c - e, 0, -1);
            Arrow(dc, c, c + e, 0, 1);
            for (int k = -half; k <= half; k++)
            {
                if (k == 0) continue;
                double p = c + k * step;
                dc.DrawLine(axisPen, new Point(p, c - 3), new Point(p, c + 3));
                dc.DrawLine(axisPen, new Point(c - 3, p), new Point(c + 3, p));
                if (!s.PadLabels || (half > 5 && k % 2 != 0)) continue;
                string tx = s.PadRaw
                    ? ((int)Math.Round(k * (double)s.PadRange / half)).ToString(CultureInfo.InvariantCulture)
                    : (k * 5.0 / half).ToString("0.#", CultureInfo.InvariantCulture);
                var fx = Text(tx, s.PadNumSize, dpi, numBrush);
                dc.DrawText(fx, new Point(p - fx.Width / 2, c + 5));
                string ty = s.PadRaw
                    ? ((int)Math.Round(-k * (double)s.PadRange / half)).ToString(CultureInfo.InvariantCulture)
                    : (-k * 5.0 / half).ToString("0.#", CultureInfo.InvariantCulture);
                var fy = Text(ty, s.PadNumSize, dpi, numBrush);
                dc.DrawText(fy, new Point(c - 6 - fy.Width, p - fy.Height / 2));
            }
            var lx = Text("x", s.PadLetterSize, dpi, letterBrush);
            dc.DrawText(lx, new Point(n - lx.Width - 4, c - lx.Height - 2));
            dc.DrawText(Text("y", s.PadLetterSize, dpi, letterBrush), new Point(c + 7, 1));
        }
        g.Freeze();
        return g;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (st == null || model == null) return;
        var s = st;
        int n = s.PadSize;
        double c = n / 2.0, h = c - 14;
        int key = Key(s);
        if (!haveStyle || key != styleKey)
        {
            Rebuild(n);
            styleKey = key;
            haveStyle = true;
        }
        dc.DrawDrawing(back!);
        var ps = model.State();
        double px = c + Math.Clamp(ps.Nx, -1, 1) * h;
        double py = c - Math.Clamp(ps.Ny, -1, 1) * h;
        if (s.PadTrailMs > 0)
        {
            long now = Stopwatch.GetTimestamp();
            long maxAge = s.PadTrailMs * Stopwatch.Frequency / 1000;
            int cnt = model.CopyTrail(scratch, now, maxAge);
            for (int i = 1; i < cnt; i++)
            {
                var a = scratch[i - 1];
                var b = scratch[i];
                if (a.X == b.X && a.Y == b.Y) continue;
                double age = (now - b.T) / (double)maxAge;
                int bi = Math.Clamp((int)((1 - age) * (trail.Length - 1)), 0, trail.Length - 1);
                dc.DrawLine(trail[bi], new Point(c + a.X * h, c - a.Y * h), new Point(c + b.X * h, c - b.Y * h));
            }
        }
        if (s.PadCross)
        {
            dc.DrawLine(crossPen, new Point(px, c), new Point(px, py));
            dc.DrawLine(crossPen, new Point(c, py), new Point(px, py));
        }
        var live = Live;
        if (s.PadGame && live != null && live.Game == Game.DR2)
            dc.DrawEllipse(null, markerPen, new Point(c + Math.Clamp(live.Steer, -1f, 1f) * h, c), 5, 5);
        dc.DrawEllipse(dotBrush, ringPen, new Point(px, py), s.PadDot, s.PadDot);
    }
}
