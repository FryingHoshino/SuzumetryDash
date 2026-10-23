using System.Buffers;
using System.Text.Json;
using Suzumetry.Core;

namespace Suzumetry.Net;

public sealed class FrameJson
{
    readonly ArrayBufferWriter<byte> buf = new(1024);
    readonly Utf8JsonWriter w;

    public FrameJson()
    {
        w = new Utf8JsonWriter(buf, new JsonWriterOptions { SkipValidation = true });
    }

    static double R(float v, int d = 2) => float.IsFinite(v) ? Math.Round(v, d) : 0d;

    void N(ReadOnlySpan<byte> k, float v, int d = 2) => w.WriteNumber(k, R(v, d));

    void I(ReadOnlySpan<byte> k, int v) => w.WriteNumber(k, v);

    void A(ReadOnlySpan<byte> k, float[] a)
    {
        w.WriteStartArray(k);
        for (int i = 0; i < a.Length; i++) w.WriteNumberValue(R(a[i], 1));
        w.WriteEndArray();
    }

    public ReadOnlyMemory<byte> Encode(Snap? s, bool active)
    {
        buf.Clear();
        w.Reset(buf);
        w.WriteStartObject();
        if (s == null || !active)
        {
            w.WriteNumber("a"u8, 0);
            w.WriteEndObject();
            w.Flush();
            return buf.WrittenMemory;
        }
        w.WriteNumber("a"u8, 1);
        I("g"u8, (int)s.Game);
        w.WriteString("c"u8, s.Car);
        w.WriteString("t"u8, s.Track);
        I("st"u8, s.Status);
        I("se"u8, s.Session);
        N("th"u8, s.Throttle);
        N("br"u8, s.Brake);
        N("cl"u8, s.Clutch);
        N("sw"u8, s.Steer);
        I("gr"u8, s.Gear);
        I("rpm"u8, s.Rpm);
        I("mr"u8, s.MaxRpm);
        N("sp"u8, s.Speed, 1);
        N("fu"u8, s.Fuel, 1);
        N("mf"u8, s.MaxFuel, 1);
        N("fpl"u8, s.FuelPerLap);
        N("gx"u8, s.GLat);
        N("gy"u8, s.GLon);
        A("tt"u8, s.TyreTemp);
        A("tp"u8, s.TyrePress);
        A("bt"u8, s.BrakeTemp);
        A("tw"u8, s.TyreWear);
        I("lp"u8, s.Lap);
        I("ps"u8, s.Position);
        I("cur"u8, s.CurMs);
        I("last"u8, s.LastMs);
        I("best"u8, s.BestMs);
        I("sec"u8, s.Sector);
        N("np"u8, s.LapPos, 4);
        N("ta"u8, s.TcAct);
        N("aa"u8, s.AbsAct);
        N("bb"u8, s.BrakeBias, 3);
        I("tl"u8, s.TcLvl);
        I("al"u8, s.AbsLvl);
        I("mp"u8, s.Map);
        I("fl"u8, s.Flag);
        w.WriteBoolean("pit"u8, s.InPit);
        w.WriteBoolean("pl"u8, s.PitLimiter);
        N("at"u8, s.AirTemp, 1);
        N("rt"u8, s.RoadTemp, 1);
        N("tb"u8, s.Boost);
        w.WriteEndObject();
        w.Flush();
        return buf.WrittenMemory;
    }
}
