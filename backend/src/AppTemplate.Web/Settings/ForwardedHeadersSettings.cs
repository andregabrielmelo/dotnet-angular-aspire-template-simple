namespace AppTemplate.Web.Settings;

/// <summary>
/// The reverse proxies whose <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> the API trusts
/// (<c>ForwardedHeaders</c> in appsettings). Only they can set the client address the rate limiter
/// partitions by; from anyone else the headers are ignored. Empty by default: Development trusts
/// loopback (the Angular dev server's proxy), and production lists its load balancer.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>IP addresses, such as <c>10.0.0.4</c> or <c>::1</c>.</summary>
    public List<string> KnownProxies { get; set; } = [];
}
