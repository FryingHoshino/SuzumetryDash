using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Suzumetry.Core;

namespace Suzumetry.Net;

public sealed class WebServer : IDisposable
{
    sealed class Client
    {
        public readonly Channel<byte[]> Ch = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(3) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    }

    readonly Hub hub;
    readonly object gate = new();
    readonly List<Client> clients = new();
    readonly byte[] page;
    TcpListener? listener;
    CancellationTokenSource? cts;
    Thread? pump;

    public string? Error { get; private set; }
    public bool Running { get; private set; }

    public int Clients
    {
        get { lock (gate) return clients.Count; }
    }

    public WebServer(Hub hub)
    {
        this.hub = hub;
        using var s = typeof(WebServer).Assembly.GetManifestResourceStream("index.html");
        if (s == null) page = Encoding.UTF8.GetBytes("missing page");
        else
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            page = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(ms.ToArray()).Replace("{{AUTHOR}}", WebUtility.HtmlEncode(Author.Name)).Replace("{{INITIAL}}", WebUtility.HtmlEncode(Author.Initial)));
        }
    }

    public void Start(int port)
    {
        Stop();
        try
        {
            var l = new TcpListener(IPAddress.Any, port);
            l.Start(64);
            listener = l;
            cts = new CancellationTokenSource();
            var ct = cts.Token;
            Error = null;
            Running = true;
            _ = Task.Run(() => AcceptLoop(l, ct));
            pump = new Thread(() => Pump(ct)) { IsBackground = true, Name = "Suzumetry.Stream" };
            pump.Start();
        }
        catch (Exception e)
        {
            Error = e.Message;
            Running = false;
            Log.Write(e);
        }
    }

    public void Stop()
    {
        Running = false;
        try { cts?.Cancel(); } catch { }
        try { listener?.Stop(); } catch { }
        pump?.Join(1500);
        pump = null;
        lock (gate)
        {
            foreach (var c in clients) c.Ch.Writer.TryComplete();
            clients.Clear();
        }
        cts?.Dispose();
        cts = null;
        listener = null;
    }

    public void Dispose() => Stop();

    async Task AcceptLoop(TcpListener l, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient c;
            try
            {
                c = await l.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch
            {
                await Task.Delay(100);
                continue;
            }
            _ = Task.Run(() => Handle(c, ct));
        }
    }

    async Task Handle(TcpClient c, CancellationToken ct)
    {
        using (c)
        {
            try
            {
                c.NoDelay = true;
                c.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var s = c.GetStream();
                var head = await ReadHead(s, ct);
                if (head == null) return;
                var lines = head.Split("\r\n");
                var parts = lines[0].Split(' ');
                if (parts.Length < 2) return;
                var path = parts[1];
                int q = path.IndexOf('?');
                if (q >= 0) path = path[..q];
                string? key = null;
                for (int i = 1; i < lines.Length; i++)
                {
                    int k = lines[i].IndexOf(':');
                    if (k <= 0) continue;
                    if (lines[i].AsSpan(0, k).Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                        key = lines[i][(k + 1)..].Trim();
                }
                if (path == "/ws" && key != null) await Upgrade(s, key, ct);
                else if (path == "/" || path == "/index.html") await Respond(s, 200, "text/html; charset=utf-8", page, ct);
                else if (path == "/api/frame") await Respond(s, 200, "application/json", new FrameJson().Encode(hub.Current, hub.Active).ToArray(), ct);
                else if (path == "/chibi" && Author.Chibi != null) await Respond(s, 200, Author.MimeOf(Author.Chibi), Author.Chibi, ct);
                else if (path == "/author.png" && Author.Image != null) await Respond(s, 200, Author.Mime, Author.Image, ct);
                else await Respond(s, 404, "text/plain", Encoding.ASCII.GetBytes("404"), ct);
            }
            catch { }
        }
    }

    static async Task<string?> ReadHead(NetworkStream s, CancellationToken ct)
    {
        using var t = CancellationTokenSource.CreateLinkedTokenSource(ct);
        t.CancelAfter(5000);
        var b = new byte[8192];
        int n = 0;
        while (n < b.Length)
        {
            int r = await s.ReadAsync(b.AsMemory(n), t.Token);
            if (r <= 0) return null;
            n += r;
            if (b.AsSpan(0, n).IndexOf("\r\n\r\n"u8) >= 0) return Encoding.ASCII.GetString(b, 0, n);
        }
        return null;
    }

    static async Task Respond(NetworkStream s, int code, string type, byte[] body, CancellationToken ct)
    {
        var reason = code == 200 ? "OK" : "Not Found";
        var h = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {reason}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        await s.WriteAsync(h, ct);
        await s.WriteAsync(body, ct);
        await s.FlushAsync(ct);
    }

    static async Task ReadExact(NetworkStream s, byte[] b, int n, CancellationToken ct)
    {
        int o = 0;
        while (o < n)
        {
            int r = await s.ReadAsync(b.AsMemory(o, n - o), ct);
            if (r <= 0) throw new IOException();
            o += r;
        }
    }

    static async Task ReadFrames(NetworkStream s, Client cl, CancellationToken ct)
    {
        var h = new byte[2];
        var ext = new byte[8];
        var skip = new byte[1024];
        while (true)
        {
            await ReadExact(s, h, 2, ct);
            int op = h[0] & 0x0F;
            bool masked = (h[1] & 0x80) != 0;
            long len = h[1] & 0x7F;
            if (len == 126)
            {
                await ReadExact(s, ext, 2, ct);
                len = (ext[0] << 8) | ext[1];
            }
            else if (len == 127)
            {
                await ReadExact(s, ext, 8, ct);
                len = BinaryPrimitives.ReadInt64BigEndian(ext);
            }
            if (masked) await ReadExact(s, ext, 4, ct);
            if (len < 0 || len > 1 << 20) return;
            while (len > 0)
            {
                int n = (int)Math.Min(len, skip.Length);
                await ReadExact(s, skip, n, ct);
                len -= n;
            }
            if (op == 8) return;
            if (op == 9) cl.Ch.Writer.TryWrite(new byte[] { 0x8A, 0 });
        }
    }

    async Task Upgrade(NetworkStream s, string key, CancellationToken ct)
    {
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var resp = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
        await s.WriteAsync(Encoding.ASCII.GetBytes(resp), ct);

        var cl = new Client();
        cl.Ch.Writer.TryWrite(Wrap(new FrameJson().Encode(hub.Current, hub.Active).Span));
        lock (gate) clients.Add(cl);

        using var lc = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tk = lc.Token;
        var wt = Task.Run(async () =>
        {
            try
            {
                await foreach (var m in cl.Ch.Reader.ReadAllAsync(tk))
                    await s.WriteAsync(m, tk);
            }
            catch { }
        });
        var rt = Task.Run(async () =>
        {
            try { await ReadFrames(s, cl, tk); }
            catch { }
        });
        await Task.WhenAny(wt, rt);
        lc.Cancel();
        lock (gate) clients.Remove(cl);
        cl.Ch.Writer.TryComplete();
        await Task.WhenAll(wt, rt);
    }

    static byte[] Wrap(ReadOnlySpan<byte> p)
    {
        int hl = p.Length < 126 ? 2 : p.Length < 65536 ? 4 : 10;
        var f = new byte[hl + p.Length];
        f[0] = 0x81;
        if (p.Length < 126) f[1] = (byte)p.Length;
        else if (p.Length < 65536)
        {
            f[1] = 126;
            f[2] = (byte)(p.Length >> 8);
            f[3] = (byte)p.Length;
        }
        else
        {
            f[1] = 127;
            BinaryPrimitives.WriteUInt64BigEndian(f.AsSpan(2), (ulong)p.Length);
        }
        p.CopyTo(f.AsSpan(hl));
        return f;
    }

    void Broadcast(byte[] frame)
    {
        lock (gate)
        {
            foreach (var c in clients) c.Ch.Writer.TryWrite(frame);
        }
    }

    void Pump(CancellationToken ct)
    {
        var enc = new FrameJson();
        long last = -1;
        bool idleSent = false;
        int hz = hub.Settings.StreamHz;
        using var pacer = new Pacer(hz);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int want = hub.Settings.StreamHz;
                if (want != hz) { hz = want; pacer.SetRate(hz); }
                if (Clients > 0)
                {
                    var f = hub.Current;
                    if (f != null && hub.Active)
                    {
                        if (f.Seq != last)
                        {
                            last = f.Seq;
                            idleSent = false;
                            Broadcast(Wrap(enc.Encode(f, true).Span));
                        }
                    }
                    else if (!idleSent)
                    {
                        idleSent = true;
                        last = -1;
                        Broadcast(Wrap(enc.Encode(null, false).Span));
                    }
                }
            }
            catch (Exception e) { Log.Write(e); }
            pacer.Wait();
        }
    }

    public static List<string> Urls(int port)
    {
        var r = new List<string>();
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var a in n.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = a.Address.ToString();
                    if (ip.StartsWith("169.254")) continue;
                    r.Add($"http://{ip}:{port}");
                }
            }
        }
        catch { }
        r.Add($"http://localhost:{port}");
        return r;
    }
}
