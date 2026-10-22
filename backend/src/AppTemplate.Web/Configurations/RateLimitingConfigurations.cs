using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Configurations;

/// <summary>Limits for the global rate limiter, per partition and per window.</summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests a signed-in user (partitioned by <c>sub</c>) may make per window.</summary>
    public int AuthenticatedPermitLimit { get; set; } = 600;

    /// <summary>Requests one anonymous client address may make per window.</summary>
    public int AnonymousPermitLimit { get; set; } = 120;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// A global limiter (ASP.NET Core rate limiting) caps overall traffic per caller. It keys on
/// <see cref="ClientPartition"/>, never on anything the client sends, and rejects with a 429
/// problem details response. For a sensitive or expensive endpoint, add a named policy with
/// <c>options.AddPolicy</c> and apply it with <c>Options(x => x.RequireRateLimiting(name))</c>.
/// </summary>
public static class RateLimitingConfigurations
{
    public static IServiceCollection AddRateLimitingConfigurations(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .Validate(
                settings =>
                    settings.AuthenticatedPermitLimit > 0
                    && settings.AnonymousPermitLimit > 0
                    && settings.Window > TimeSpan.Zero,
                "RateLimiting needs positive permit limits and a positive window."
            )
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        // Read from the registered settings when the options are built, never at registration.
        services
            .AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingSettings>>(
                (options, registered) =>
                {
                    var settings = registered.Value;
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                        context =>
                        {
                            if (ClientPartition.IsExcluded(context.Request.Path))
                            {
                                return RateLimitPartition.GetNoLimiter(string.Empty);
                            }

                            var partition = ClientPartition.Of(context);
                            var permitLimit = partition.StartsWith(
                                ClientPartition.UserPrefix,
                                StringComparison.Ordinal
                            )
                                ? settings.AuthenticatedPermitLimit
                                : settings.AnonymousPermitLimit;

                            return FixedWindow(partition, permitLimit, settings.Window);
                        }
                    );

                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    options.OnRejected = (context, _) =>
                        WriteTooManyRequestsAsync(
                            context.HttpContext,
                            context.Lease.TryGetMetadata(
                                MetadataName.RetryAfter,
                                out var retryAfter
                            )
                                ? retryAfter
                                : settings.Window
                        );
                }
            );

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(
        string partition,
        int permitLimit,
        TimeSpan window
    ) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partition,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            }
        );

    /// <summary>A 429 problem details response with a <c>Retry-After</c> in whole seconds.</summary>
    private static async ValueTask WriteTooManyRequestsAsync(
        HttpContext context,
        TimeSpan retryAfter
    )
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds)
            )
            .ToString(CultureInfo.InvariantCulture);

        await context
            .RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail =
                            "The rate limit was exceeded. Retry after the time in Retry-After.",
                    },
                }
            );
    }
}

/// <summary>
/// Who a request counts against: <c>user:{sub}</c> once JWT authentication has validated the
/// token, otherwise <c>ip:{address}</c> of the connection. The address is rewritten from
/// <c>X-Forwarded-For</c> only when the connection comes from a configured proxy (see
/// <see cref="ForwardedHeadersSettings"/>), so a client can't choose its own partition. The
/// rate limiter must run after <c>UseAuthentication</c> for <c>sub</c> to be known.
/// </summary>
public static class ClientPartition
{
    public const string UserPrefix = "user:";
    public const string AddressPrefix = "ip:";

    private static readonly string[] ExcludedPaths = ["/health", "/alive"];

    public static bool IsExcluded(PathString path) =>
        ExcludedPaths.Any(excluded => path.StartsWithSegments(excluded));

    public static string Of(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(subject))
            {
                return UserPrefix + subject;
            }
        }

        return AddressPrefix + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
