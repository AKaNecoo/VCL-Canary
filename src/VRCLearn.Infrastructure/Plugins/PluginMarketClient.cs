using System.Net;
using System.Net.Http;

namespace VRCLearn.Infrastructure.Plugins;

public sealed class PluginMarketClient : HttpClient
{
    private const string Host = "vclplugin.gamemode1.top";
    public const string Market = "https://" + Host + "/";

    public PluginMarketClient() : this(new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    {
    }

    internal PluginMarketClient(HttpMessageHandler handler) : base(handler)
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        DefaultRequestHeaders.UserAgent.ParseAdd("VRCLearn");
    }

    public static bool Matches(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
        && uri.IdnHost.Equals(Host, StringComparison.OrdinalIgnoreCase);
}
