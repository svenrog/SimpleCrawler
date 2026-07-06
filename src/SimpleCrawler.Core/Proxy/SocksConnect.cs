using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace SimpleCrawler.Core.Proxy;

internal static class SocksConnect
{
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        ProxyInfo proxy,
        CancellationToken cancellationToken)
    {
        var target = context.DnsEndPoint;

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new DnsEndPoint(proxy.Host, proxy.Port), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        var stream = new NetworkStream(socket, ownsSocket: true);

        try
        {
            if (proxy.Protocol == ProxyProtocol.Socks5)
                await Socks5HandshakeAsync(stream, target.Host, target.Port, proxy, cancellationToken).ConfigureAwait(false);
            else
                await Socks4aHandshakeAsync(stream, target.Host, target.Port, proxy, cancellationToken).ConfigureAwait(false);

            if (IsTls(context))
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = target.Host }, cancellationToken).ConfigureAwait(false);
                return ssl;
            }

            return stream;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsTls(SocketsHttpConnectionContext context)
    {
        var uri = context.InitialRequestMessage.RequestUri;
        return uri is not null && uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task Socks5HandshakeAsync(Stream stream, string host, int port, ProxyInfo proxy, CancellationToken cancellationToken)
    {
        var offers = proxy.HasCredentials ? new byte[] { 0x00, 0x02 } : new byte[] { 0x00 };
        var greeting = new byte[2 + offers.Length];
        greeting[0] = 0x05;
        greeting[1] = (byte)offers.Length;
        Buffer.BlockCopy(offers, 0, greeting, 2, offers.Length);
        await stream.WriteAsync(greeting, cancellationToken).ConfigureAwait(false);

        var selection = new byte[2];
        await ReadExactAsync(stream, selection, cancellationToken).ConfigureAwait(false);
        if (selection[0] != 0x05)
            throw new IOException("SOCKS5 greeting rejected by proxy.");

        var method = selection[1];
        if (method == 0x02)
        {
            if (!proxy.HasCredentials)
                throw new IOException("SOCKS5 proxy requires authentication but none was configured.");

            await AuthenticateUserPassAsync(stream, proxy.Username!, proxy.Password, cancellationToken).ConfigureAwait(false);
        }
        else if (method != 0x00)
        {
            throw new IOException("SOCKS5 proxy offered no supported authentication method.");
        }

        var hostBytes = Encoding.ASCII.GetBytes(host);
        var connect = new byte[7 + hostBytes.Length];
        connect[0] = 0x05;
        connect[1] = 0x01;
        connect[2] = 0x00;
        connect[3] = 0x03;
        connect[4] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, connect, 5, hostBytes.Length);
        connect[5 + hostBytes.Length] = (byte)((port >> 8) & 0xFF);
        connect[6 + hostBytes.Length] = (byte)(port & 0xFF);
        await stream.WriteAsync(connect, cancellationToken).ConfigureAwait(false);

        var head = new byte[4];
        await ReadExactAsync(stream, head, cancellationToken).ConfigureAwait(false);
        if (head[0] != 0x05)
            throw new IOException("Malformed SOCKS5 reply.");
        if (head[1] != 0x00)
            throw new IOException($"SOCKS5 CONNECT failed (reply code {head[1]}).");

        var addressLength = head[3] switch
        {
            0x01 => 4,
            0x04 => 16,
            0x03 => await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false),
            _ => throw new IOException($"Unknown SOCKS5 address type {head[3]}."),
        };

        await DrainAsync(stream, addressLength + 2, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AuthenticateUserPassAsync(Stream stream, string user, string? pass, CancellationToken cancellationToken)
    {
        var userBytes = Encoding.ASCII.GetBytes(user);
        var passBytes = Encoding.ASCII.GetBytes(pass ?? string.Empty);
        var packet = new byte[3 + userBytes.Length + passBytes.Length];
        packet[0] = 0x01;
        packet[1] = (byte)userBytes.Length;
        Buffer.BlockCopy(userBytes, 0, packet, 2, userBytes.Length);
        packet[2 + userBytes.Length] = (byte)passBytes.Length;
        Buffer.BlockCopy(passBytes, 0, packet, 3 + userBytes.Length, passBytes.Length);
        await stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);

        var status = new byte[2];
        await ReadExactAsync(stream, status, cancellationToken).ConfigureAwait(false);
        if (status[1] != 0x00)
            throw new IOException("SOCKS5 username/password authentication failed.");
    }

    private static async Task Socks4aHandshakeAsync(Stream stream, string host, int port, ProxyInfo proxy, CancellationToken cancellationToken)
    {
        var userBytes = Encoding.ASCII.GetBytes(proxy.Username ?? string.Empty);
        var hostBytes = Encoding.ASCII.GetBytes(host);
        var packet = new byte[9 + userBytes.Length + hostBytes.Length];
        packet[0] = 0x04;
        packet[1] = 0x01;
        packet[2] = (byte)((port >> 8) & 0xFF);
        packet[3] = (byte)(port & 0xFF);
        packet[4] = 0x00;
        packet[5] = 0x00;
        packet[6] = 0x00;
        packet[7] = 0x01; // SOCKS4a hostname marker (non-resolvable IP)
        Buffer.BlockCopy(userBytes, 0, packet, 8, userBytes.Length);
        packet[8 + userBytes.Length] = 0x00; // userid terminator
        Buffer.BlockCopy(hostBytes, 0, packet, 9 + userBytes.Length, hostBytes.Length);
        packet[^1] = 0x00; // hostname terminator
        await stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);

        var reply = new byte[8];
        await ReadExactAsync(stream, reply, cancellationToken).ConfigureAwait(false);
        if (reply[1] != 0x5A)
            throw new IOException($"SOCKS4a CONNECT failed (status {reply[1]:X2}).");
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var remaining = buffer.Length;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(buffer.Length - remaining, remaining), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Proxy closed the connection during handshake.");
            remaining -= read;
        }
    }

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var single = new byte[1];
        await ReadExactAsync(stream, single, cancellationToken).ConfigureAwait(false);
        return single[0];
    }

    private static async Task DrainAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        var chunk = new byte[Math.Min(count, 64)];
        while (count > 0)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(count, chunk.Length)), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Proxy closed the connection during handshake.");
            count -= read;
        }
    }
}
