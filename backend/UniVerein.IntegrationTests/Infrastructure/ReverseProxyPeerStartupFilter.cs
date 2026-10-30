using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace UniVerein.IntegrationTests.Infrastructure;

// The test server has no network peer. Production always sits behind the nginx container (optionally behind Caddy
// in front of it), which hands the client address over in X-Real-IP, so tests act as that proxy and the real
// forwarded-header handling of the API decides what the client address is.
internal sealed class ReverseProxyPeerStartupFilter : IStartupFilter
{
    public static readonly IPAddress ProxyAddress = IPAddress.Parse("172.18.0.2");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= ProxyAddress;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
