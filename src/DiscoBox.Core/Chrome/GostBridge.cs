using System.Diagnostics;
using System.Net.Sockets;
using DiscoBox.Core.Logging;
using DiscoBox.Core.Net;

namespace DiscoBox.Core.Chrome;

public sealed class GostBridge : IDisposable
{
    private readonly Process process;
    private readonly FileLogger logger;
    private bool disposed;

    private GostBridge(Process process, int localPort, FileLogger logger)
    {
        this.process = process;
        LocalPort = localPort;
        this.logger = logger;
    }

    public int LocalPort { get; }

    public string ProxyServerArg => $"127.0.0.1:{LocalPort}";

    public static async Task<GostBridge> StartAsync(string gostPath, ProxyEndpoint upstream,
        FileLogger logger, CancellationToken token)
    {
        var localPort = FreePort();
        var start = new ProcessStartInfo
        {
            FileName = gostPath,
            Arguments = $"-L http://127.0.0.1:{localPort} -F {upstream.Url}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var process = Process.Start(start)
                      ?? throw new InvalidOperationException("failed to start gost bridge");

        var bridge = new GostBridge(process, localPort, logger);
        _ = Task.Run(() => bridge.Drain(process.StandardOutput, "stdout"), CancellationToken.None);
        _ = Task.Run(() => bridge.Drain(process.StandardError, "stderr"), CancellationToken.None);
        logger.Info("gost", $"bridge started on 127.0.0.1:{localPort} -> {upstream.MaskedUrl} (pid {process.Id})");

        await bridge.WaitListeningAsync(token);
        return bridge;
    }

    private async Task WaitListeningAsync(CancellationToken token)
    {
        // HACK: probing the port beats parsing gost stdout, its log format moves between releases
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException($"gost bridge exited with code {process.ExitCode}");
            }

            try
            {
                using var probe = new TcpClient();
                await probe.ConnectAsync("127.0.0.1", LocalPort, token);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(150, token);
            }
        }

        throw new TimeoutException("gost bridge did not start listening in 10s");
    }

    private void Drain(StreamReader reader, string stream)
    {
        try
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                logger.Debug("gost", $"{stream}: {line}");
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
            logger.Info("gost", $"bridge on port {LocalPort} stopped");
        }
        catch (Exception exception)
        {
            logger.Warn("gost", $"bridge stop issue: {exception.Message}");
        }
    }

    internal static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
