using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// The test server has no real TCP peer, so this sets <c>RemoteIpAddress</c> before the app's
/// own middleware runs: <see cref="TrustedProxy"/> (a stand-in for the backend for frontend)
/// unless the request names another peer in <see cref="HeaderName"/>, which lets a test act
/// as a client connecting directly from an untrusted address.
/// </summary>
public sealed class TestPeerAddress : IStartupFilter
{
    public const string HeaderName = "X-Test-Peer-Address";
    public const string TrustedProxy = "127.0.0.1";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(
                async (context, nextMiddleware) =>
                {
                    var peer = context.Request.Headers[HeaderName].ToString();
                    context.Connection.RemoteIpAddress = IPAddress.Parse(
                        string.IsNullOrEmpty(peer) ? TrustedProxy : peer
                    );
                    await nextMiddleware(context);
                }
            );
            next(app);
        };
}
