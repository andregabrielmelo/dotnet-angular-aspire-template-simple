using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Configurations;

/// <summary>
/// The proxies allowed to report the client's address in <c>X-Forwarded-For</c>, such as the
/// backend for frontend or a load balancer. Empty by default: unless the connection comes
/// from one of these, the header is ignored and <c>RemoteIpAddress</c> stays the real peer, so
/// a client can't pick its own rate-limit partition by sending the header itself.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Individual proxy addresses, such as <c>10.0.0.5</c> or <c>::1</c>.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Proxy networks in CIDR notation, such as <c>10.0.0.0/8</c>.</summary>
    public string[] KnownNetworks { get; set; } = [];
}

public static class ForwardedHeadersConfigurations
{
    public static IServiceCollection AddForwardedHeadersConfigurations(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection(ForwardedHeadersSettings.SectionName))
            .Validate(
                settings => settings.KnownProxies.All(proxy => IPAddress.TryParse(proxy, out _)),
                "ForwardedHeaders:KnownProxies must be IP addresses."
            )
            .Validate(
                settings =>
                    settings.KnownNetworks.All(network =>
                        System.Net.IPNetwork.TryParse(network, out _)
                    ),
                "ForwardedHeaders:KnownNetworks must be CIDR networks, such as 10.0.0.0/8."
            )
            .ValidateOnStart();

        // Read from the registered settings when the options are built, never at registration.
        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>(
                (options, registered) =>
                {
                    var settings = registered.Value;
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    // ASP.NET Core trusts loopback by default; trust exactly what is configured instead.
                    options.KnownProxies.Clear();
                    options.KnownIPNetworks.Clear();
                    foreach (var proxy in settings.KnownProxies)
                    {
                        options.KnownProxies.Add(IPAddress.Parse(proxy));
                    }
                    foreach (var network in settings.KnownNetworks)
                    {
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                    }
                }
            );

        return services;
    }
}
