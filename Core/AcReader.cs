using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace Suzumetry.Core;

public sealed class AcReader : ISource
{
    const int PhysLen = 576;
    const int GfxLen = 1296;
    const int StaLen = 424;

    readonly Hub hub;
    readonly Game game;
    Thread? thread;
    volatile bool run;
    MemoryMappedFile? pf, gf, sf;
    MemoryMappedViewAccessor? pa, ga, sa;

    public AcReader(Hub hub, Game game)
    {
        this.hub = hub;
        this.game = game;
    }

    public void Start()
    {
        run = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "Suzumetry." + game };
        thread.Start();
    }

    public void Stop()
    {
        run = false;
        thread?.Join(2000);
        thread = null;
    }

    bool TryOpen()
    {
        try
        {
            pf = MemoryMappedFile.OpenExisting("Local\\acpmf_physics", MemoryMappedFileRights.Read);
            gf = MemoryMappedFile.OpenExisting("Local\\acpmf_graphics", MemoryMappedFileRights.Read);
            sf = MemoryMappedFile.OpenExisting("Local\\acpmf_static", MemoryMappedFileRights.Read);
            pa = pf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            ga = gf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            sa = sf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return true;
        }
        catch
        {
            Close();
            return false;
        }
    }

    void Close()
    {
        pa?.Dispose(); ga?.Dispose(); sa?.Dispose();
        pa = null; ga = null; sa = null;
        pf?.Dispose(); gf?.Dispose(); sf?.Dispose();
        pf = null; gf = null; sf = null;
    }

    static int Fill(MemoryMappedViewAccessor a, byte[] buf)
    {
        int n = (int)Math.Min(a.Capacity, buf.Length);
        a.ReadArray(0, buf, 0, n);
        return n;
    }

    void Loop()
    {
        var pb = new byte[PhysLen];
        var gb = new byte[GfxLen];
        var sb = new byte[StaLen];
        using var pacer = new Pacer(hub.Settings.ReadHz);
        int hz = hub.Settings.ReadHz;
        int lastPid = -1;
        long lastPub = 0, lastSta = 0;
        string car = "", track = "";
        int maxRpm = 0;
        float maxFuel = 0;

        while (run)
        {
            try
            {
                if (pa == null)
                {
                    if (!TryOpen())
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    lastSta = 0;
                }
                int want = hub.Settings.ReadHz;
                if (want != hz) { hz = want; pacer.SetRate(hz); }
                long now = Stopwatch.GetTimestamp();
                int pn = Fill(pa!, pb);
                int pid = Bin.I(pb, 0);
                if (pid == lastPid && now - lastPub < Stopwatch.Frequency / 4)
                {
                    pacer.Wait();
                    continue;
                }
                int gn = Fill(ga!, gb);
                if (lastSta == 0 || now - lastSta > Stopwatch.Frequency)
                {
                    Fill(sa!, sb);
                    car = Bin.S(sb, 68, 33);
                    track = Bin.S(sb, 134, 33);
                    maxRpm = Bin.I(sb, 412);
                    maxFuel = Bin.F(sb, 416);
                    lastSta = now;
                }

                var s = new Snap { Game = game, Car = car, Track = track, MaxRpm = maxRpm, MaxFuel = maxFuel };
                s.Throttle = Bin.F(pb, 4);
                s.Brake = Bin.F(pb, 8);
                s.Fuel = Bin.F(pb, 12);
                s.Gear = Bin.I(pb, 16) - 1;
                s.Rpm = Bin.I(pb, 20);
                s.Steer = Bin.F(pb, 24);
                s.Speed = Bin.F(pb, 28);
                s.GLat = Bin.F(pb, 44);
                s.GLon = Bin.F(pb, 52);
                for (int i = 0; i < 4; i++)
                {
                    s.TyrePress[i] = Bin.F(pb, 88 + i * 4);
                    s.TyreWear[i] = Bin.F(pb, 120 + i * 4);
                    s.TyreTemp[i] = Bin.F(pb, 152 + i * 4);
                    s.BrakeTemp[i] = pn >= 364 ? Bin.F(pb, 348 + i * 4) : 0f;
                }
                s.TcAct = Bin.F(pb, 204);
                s.AbsAct = Bin.F(pb, 252);
                s.PitLimiter = Bin.I(pb, 248) != 0;
                s.Boost = Bin.F(pb, 276);
                s.AirTemp = Bin.F(pb, 288);
                s.RoadTemp = Bin.F(pb, 292);
                s.Clutch = pn >= 368 ? 1f - Bin.F(pb, 364) : 0f;
                if (game == Game.ACC && pn >= 568) s.BrakeBias = Bin.F(pb, 564);

                s.Status = Bin.I(gb, 4);
                s.Session = Bin.I(gb, 8);
                s.Lap = Bin.I(gb, 132);
                s.Position = Bin.I(gb, 136);
                s.CurMs = Bin.I(gb, 140);
                s.LastMs = Bin.I(gb, 144);
                s.BestMs = Bin.I(gb, 148);
                s.InPit = Bin.I(gb, 160) != 0;
                s.Sector = Bin.I(gb, 164);
                s.LapPos = Bin.F(gb, 248);
                if (gn >= 1240)
                {
                    s.Flag = Bin.I(gb, 1224);
                    if (Bin.I(gb, 1236) != 0) s.InPit = true;
                }
                if (game == Game.ACC && gn >= 1288)
                {
                    s.TcLvl = Bin.I(gb, 1268);
                    s.Map = Bin.I(gb, 1276);
                    s.AbsLvl = Bin.I(gb, 1280);
                    s.FuelPerLap = Bin.F(gb, 1284);
                }
                hub.Publish(s);
                lastPid = pid;
                lastPub = now;
            }
            catch (Exception e)
            {
                Log.Write(e);
                Close();
                Thread.Sleep(500);
                continue;
            }
            pacer.Wait();
        }
        Close();
    }
}
