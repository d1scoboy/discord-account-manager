using System.Net.Sockets;
using System.Text;

namespace DiscoBox.Core.Net;

public static class Socks5
{
    // TODO socks4a support if some provider ever shows up with it
    public static async Task<NetworkStream> ConnectAsync(
        string proxyHost, int proxyPort, string targetHost, int targetPort,
        string? user, string? password, CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(proxyHost, proxyPort, token);
        var stream = new NetworkStream(socket, ownsSocket: true);
        try
        {
            await HandshakeAsync(stream, user, password, token);
            await SendConnectAsync(stream, targetHost, targetPort, token);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }

        return stream;
    }

    private static async Task HandshakeAsync(NetworkStream stream, string? user, string? password,
        CancellationToken token)
    {
        var hasAuth = user is not null;
        var greeting = new byte[] { 0x05, 0x02, 0x00, hasAuth ? (byte)0x02 : (byte)0x00 };
        await stream.WriteAsync(greeting, token);

        var choice = new byte[2];
        await ReadExactAsync(stream, choice, token);
        if (choice[0] != 0x05)
        {
            throw new IOException($"SOCKS5: bad protocol version {choice[0]}");
        }

        switch (choice[1])
        {
            case 0x00:
                return;
            case 0x02:
                break;
            default:
                throw new IOException($"SOCKS5: unsupported auth method {choice[1]}");
        }

        if (!hasAuth)
        {
            throw new IOException("SOCKS5: proxy demands password auth but none configured");
        }

        var userBytes = Encoding.ASCII.GetBytes(user!);
        var passBytes = Encoding.ASCII.GetBytes(password ?? "");
        var packet = new byte[3 + userBytes.Length + passBytes.Length];
        packet[0] = 0x01;
        packet[1] = (byte)userBytes.Length;
        userBytes.CopyTo(packet, 2);
        packet[2 + userBytes.Length] = (byte)passBytes.Length;
        passBytes.CopyTo(packet, 3 + userBytes.Length);
        await stream.WriteAsync(packet, token);

        var reply = new byte[2];
        await ReadExactAsync(stream, reply, token);
        if (reply[1] != 0x00)
        {
            throw new IOException("SOCKS5: credentials rejected by proxy");
        }
    }

    private static async Task SendConnectAsync(NetworkStream stream, string targetHost, int targetPort,
        CancellationToken token)
    {
        byte[] address;
        if (System.Net.IPAddress.TryParse(targetHost, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            address = new byte[1 + bytes.Length];
            address[0] = ip.AddressFamily == AddressFamily.InterNetwork ? (byte)0x01 : (byte)0x04;
            bytes.CopyTo(address, 1);
        }
        else
        {
            var hostBytes = Encoding.ASCII.GetBytes(targetHost);
            address = new byte[2 + hostBytes.Length];
            address[0] = 0x03;
            address[1] = (byte)hostBytes.Length;
            hostBytes.CopyTo(address, 2);
        }

        var request = new byte[3 + address.Length + 2];
        request[0] = 0x05;
        request[1] = 0x01;
        request[2] = 0x00;
        address.CopyTo(request, 3);
        request[^2] = (byte)(targetPort >> 8);
        request[^1] = (byte)(targetPort & 0xFF);
        await stream.WriteAsync(request, token);

        var header = new byte[4];
        await ReadExactAsync(stream, header, token);
        if (header[1] != 0x00)
        {
            throw new IOException($"SOCKS5: connect refused, reply code {header[1]}");
        }

        var skip = header[3] switch
        {
            0x01 => 4,
            0x04 => 16,
            0x03 => 1 + (await ReadByteAsync(stream, token)),
            _ => throw new IOException($"SOCKS5: unknown address type {header[3]}"),
        };
        await ReadExactAsync(stream, new byte[skip + 2], token);
    }

    private static async Task<byte> ReadByteAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        await ReadExactAsync(stream, buffer, token);
        return buffer[0];
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token);
            if (read == 0)
            {
                throw new IOException("SOCKS5: connection closed by proxy");
            }

            offset += read;
        }
    }
}
