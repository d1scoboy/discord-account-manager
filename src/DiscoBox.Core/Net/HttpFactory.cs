using System.Net;
using System.Net.Sockets;

namespace DiscoBox.Core.Net;

public static class HttpFactory
{
    public static HttpClient Create(ProxyEndpoint? proxy, int timeoutSeconds = 20)
    {
        // tried HttpClientHandler first, no socks5 support there, keeping SocketsHttpHandler
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseCookies = false,
            AllowAutoRedirect = true,
        };

        if (proxy is not null)
        {
            if (proxy.IsSocks)
            {
                handler.ConnectCallback = async (context, token) =>
                {
                    var stream = await Socks5.ConnectAsync(
                        proxy.Host, proxy.Port,
                        context.DnsEndPoint.Host, context.DnsEndPoint.Port,
                        proxy.User, proxy.Password, token);
                    return stream;
                };
            }
            else
            {
                handler.UseProxy = true;
                var webProxy = new WebProxy(proxy.Host, proxy.Port);
                if (proxy.User is not null)
                {
                    webProxy.Credentials = new NetworkCredential(proxy.User, proxy.Password);
                }

                handler.Proxy = webProxy;
            }
        }

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
    }

    public static HttpClient CreateDirect(int timeoutSeconds = 20) => Create(null, timeoutSeconds);
}
