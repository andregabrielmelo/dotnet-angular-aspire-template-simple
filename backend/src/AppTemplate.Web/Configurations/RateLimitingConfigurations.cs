using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Configurations;

/// <summary>
/// ASP.NET Core's rate limiter (not FastEndpoints' <c>Throttle</c>, which trusts a client-sent
/// header). A global limiter covers every request; <see cref="AuthenticationPolicy"/> adds a
/// stricter one to the auth endpoints. Partitions never come from a header a client chooses: a
/// signed-in caller counts against their session's user id, anyone else against their address,
/// which is taken from <c>X-Forwarded-For</c> only when a known proxy sent it. See ADR 014.
/// </summary>
public static class RateLimitingConfigurations
{
    /// <summary>The policy of sign-in, registration and the account email endpoints.</summary>
    public const string AuthenticationPolicy = "authentication";

    public static IServiceCollection AddRateLimitingConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger
    )
    {
        services.AddRateLimiter(_ => { });
        services
            .AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitSettings>>(
                (options, settings) =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                        context =>
                        {
                            // Authentication runs first, so a validated session's user id is known here.
                            var userId = context.User.FindFirst(AppClaimTypes.UserId)?.Value;
                            return userId is null
                                ? FixedWindow(
                                    $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                                    settings.Value.AnonymousPermitLimit
                                )
                                : FixedWindow(
                                    $"user:{userId}",
                                    settings.Value.AuthenticatedPermitLimit
                                );
                        }
                    );

                    options.AddPolicy(
                        AuthenticationPolicy,
                        context =>
                            FixedWindow(
                                $"auth:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                                settings.Value.AuthenticationPermitLimit
                            )
                    );

                    options.OnRejected = async (rejected, cancellationToken) =>
                    {
                        if (
                            rejected.Lease.TryGetMetadata(
                                MetadataName.RetryAfter,
                                out var retryAfter
                            )
                        )
                        {
                            rejected.HttpContext.Response.Headers.RetryAfter = (
                                (int)Math.Ceiling(retryAfter.TotalSeconds)
                            ).ToString(CultureInfo.InvariantCulture);
                        }

                        await TypedResults
                            .Problem(
                                detail: "Too many requests. Try again later.",
                                statusCode: StatusCodes.Status429TooManyRequests
                            )
                            .ExecuteAsync(rejected.HttpContext);
                    };

                    RateLimitPartition<string> FixedWindow(string partition, int permitLimit) =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partition,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = permitLimit,
                                Window = settings.Value.Window,
                                QueueLimit = 0,
                            }
                        );
                }
            );

        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>(
                (options, settings) =>
                {
                    // Only the proxies listed in settings, not ASP.NET Core's loopback default.
                    options.KnownProxies.Clear();
                    options.KnownIPNetworks.Clear();
                    foreach (var proxy in settings.Value.KnownProxies)
                    {
                        options.KnownProxies.Add(IPAddress.Parse(proxy));
                    }

                    // With no known proxy at all, the middleware would trust every peer, so any
                    // client could pick its own address (and rate limit partition). Forward
                    // nothing then.
                    options.ForwardedHeaders =
                        options.KnownProxies.Count == 0
                            ? ForwardedHeaders.None
                            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                }
            );

        logger.LogInformation("{Project} were configured", "Rate limiting and forwarded headers");
        return services;
    }
}
