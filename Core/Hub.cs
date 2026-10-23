using System.Diagnostics;

namespace Suzumetry.Core;

public interface ISource
{
    void Start();
    void Stop();
}

public sealed class Hub : IDisposable
{
    readonly ManualResetEventSlim stop = new(false);
    Snap? current;
    long lastTick;
    long seq;
    ISource? source;
    volatile int gameId;
    Thread? watcher;

    public Settings Settings { get; }
    public Game Game => (Game)gameId;
    public Snap? Current => Volatile.Read(ref current);
    public bool Active => Stopwatch.GetTimestamp() - Interlocked.Read(ref lastTick) < Stopwatch.Frequency;

    public Hub(Settings settings) { Settings = settings; }

    public void Publish(Snap s)
    {
        s.Seq = Interlocked.Increment(ref seq);
        Volatile.Write(ref current, s);
        Interlocked.Exchange(ref lastTick, Stopwatch.GetTimestamp());
    }

    public void Start()
    {
        watcher = new Thread(Watch) { IsBackground = true, Name = "Suzumetry.Watch", Priority = ThreadPriority.BelowNormal };
        watcher.Start();
    }

    void Watch()
    {
        while (!stop.IsSet)
        {
            try
            {
                var g = Detect();
                if (g != Game) Switch(g);
            }
            catch (Exception e) { Log.Write(e); }
            stop.Wait(1000);
        }
    }

    Game Detect()
    {
        var forced = (Game)Settings.ForceGame;
        if (forced != Game.None) return forced;
        if (Has("AC2-Win64-Shipping")) return Game.ACC;
        if (Has("acs") || Has("acs_x86")) return Game.AC;
        if (Has("dirtrally2")) return Game.DR2;
        return Game.None;
    }

    static bool Has(string name)
    {
        var p = Process.GetProcessesByName(name);
        bool r = p.Length > 0;
        foreach (var x in p) x.Dispose();
        return r;
    }

    void Switch(Game g)
    {
        source?.Stop();
        source = null;
        Volatile.Write(ref current, null);
        gameId = (int)g;
        if (g == Game.AC || g == Game.ACC) source = new AcReader(this, g);
        else if (g == Game.DR2) source = new Dr2Reader(this);
        source?.Start();
    }

    public void Dispose()
    {
        stop.Set();
        watcher?.Join(2000);
        source?.Stop();
        source = null;
    }
}
