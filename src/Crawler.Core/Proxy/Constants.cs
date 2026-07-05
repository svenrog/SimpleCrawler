namespace Crawler.Core.Proxy;

public class Connect
{
    public static readonly byte[] Socks4 = [0x04, 0x01];
    public static readonly byte[] Socks5 = [0x05, 0x01, 0x00];

    public static readonly string Http =
        "CONNECT example.com:443 HTTP/1.1\r\n" +
            "Host: example.com\r\n\r\n";
}

public class Reply
{
    public static readonly byte[] Socks4 = [0x00, 0x5a];
    public static readonly byte[] Socks5 = [0x05, 0x00];

    public static readonly string Http = "HTTP/";
}