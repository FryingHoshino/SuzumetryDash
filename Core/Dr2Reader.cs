using System.Net;
using System.Net.Sockets;

namespace Suzumetry.Core;

public sealed class Dr2Reader : ISource
{
    readonly Hub hub;
    Thread? thread;
    volatile bool run;
    volatile Socket? sock;

    public Dr2Reader(Hub hub) { this.hub = hub; }

    public void Start()
    {
        run = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "Suzumetry.DR2" };
        thread.Start();
    }

    public void Stop()
    {
        run = false;
        try { sock?.Close(); } catch { }
        thread?.Join(2000);
        thread = null;
    }

    void Loop()
    {
        var buf = new byte[1024];
        var fl = new float[256];
        int bound = -1;
        int best = 0;
        while (run)
        {
            try
            {
                int port = hub.Settings.Dr2Port;
                if (sock == null || bound != port)
                {
                    sock?.Close();
                    var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    s.ReceiveTimeout = 500;
                    s.Bind(new IPEndPoint(IPAddress.Any, port));
                    sock = s;
                    bound = port;
                }
                int n = sock.Receive(buf);
                int cnt = Math.Min(n / 4, fl.Length);
                if (cnt < 40) continue;
                Buffer.BlockCopy(buf, 0, fl, 0, cnt * 4);
                float G(int i) => i < cnt && float.IsFinite(fl[i]) ? fl[i] : 0f;

                var s2 = new Snap { Game = Game.DR2, Car = "DiRT Rally 2.0" };
                s2.Speed = G(7) * 3.6f;
                s2.Throttle = G(29);
                s2.Steer = G(30);
                s2.Brake = G(31);
                s2.Clutch = G(32);
                float g = G(33);
                s2.Gear = g < 0f || g >= 9f ? -1 : (int)g;
                s2.GLat = G(34);
                s2.GLon = G(35);
                s2.Lap = (int)G(59);
                s2.Rpm = (int)(G(37) * 10f);
                s2.MaxRpm = (int)(G(63) * 10f);
                s2.Position = (int)G(39);
                s2.TcAct = G(43);
                s2.AbsAct = G(44);
                s2.Fuel = G(45);
                s2.MaxFuel = G(46);
                s2.InPit = G(47) > 0.5f;
                s2.Sector = (int)G(48);
                s2.CurMs = (int)(G(1) * 1000f);
                s2.LastMs = (int)(G(62) * 1000f);
                if (s2.LastMs > 0 && (best == 0 || s2.LastMs < best)) best = s2.LastMs;
                s2.BestMs = best;
                float len = G(61);
                s2.LapPos = len > 0f ? Math.Clamp(G(2) / len, 0f, 1f) : 0f;
                s2.BrakeTemp[0] = G(53); s2.BrakeTemp[1] = G(54); s2.BrakeTemp[2] = G(51); s2.BrakeTemp[3] = G(52);
                s2.TyrePress[0] = G(57); s2.TyrePress[1] = G(58); s2.TyrePress[2] = G(55); s2.TyrePress[3] = G(56);
                hub.Publish(s2);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
            {
            }
            catch (Exception e)
            {
                if (!run) break;
                Log.Write(e);
                try { sock?.Close(); } catch { }
                sock = null;
                bound = -1;
                Thread.Sleep(500);
            }
        }
        try { sock?.Close(); } catch { }
        sock = null;
    }
}
