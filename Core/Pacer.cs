using System.Diagnostics;

namespace Suzumetry.Core;

public sealed class Pacer : IDisposable
{
    readonly IntPtr timer;
    long period;
    long next;

    public Pacer(double hz)
    {
        timer = Native.CreateWaitableTimerExW(IntPtr.Zero, null, 0x2, 0x1F0003);
        SetRate(hz);
        next = Stopwatch.GetTimestamp();
    }

    public void SetRate(double hz)
    {
        hz = Math.Clamp(hz, 1, 1000);
        period = (long)(Stopwatch.Frequency / hz);
    }

    public void Wait()
    {
        long now = Stopwatch.GetTimestamp();
        next += period;
        if (next <= now)
        {
            if (now - next > period * 4) next = now;
            return;
        }
        long remain = next - now;
        if (timer != IntPtr.Zero)
        {
            long due = -(long)(remain * 10000000.0 / Stopwatch.Frequency);
            if (due == 0) due = -1;
            if (Native.SetWaitableTimer(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
            {
                Native.WaitForSingleObject(timer, 1000);
                return;
            }
        }
        Thread.Sleep(Math.Max(1, (int)(remain * 1000 / Stopwatch.Frequency)));
    }

    public void Dispose()
    {
        if (timer != IntPtr.Zero) Native.CloseHandle(timer);
    }
}
