using System.Diagnostics;
using AppTemplate.Core.Interfaces;
using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.Web.Middleware;

/// <summary>
/// Tags a signed-in request with its user's profile id, as NimblePros/eShopOnWeb does: the
/// request's trace gets a <c>user.id</c> tag, and every log written while handling it gets a
/// <c>UserId</c> property, so the Aspire dashboard (or any OpenTelemetry backend) can filter both
/// by user. Runs right after authentication. Only the id, which isn't personal data: never the
/// email or the name.
/// </summary>
public sealed class UserContextEnrichmentMiddleware(RequestDelegate next)
{
    /// <summary>The trace tag (OpenTelemetry's <c>user.id</c> attribute).</summary>
    public const string UserIdTag = "user.id";

    /// <summary>The log scope's property.</summary>
    public const string UserIdProperty = "UserId";

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        ILoggerAdapter<UserContextEnrichmentMiddleware> logger
    )
    {
        if (currentUser.Id is not { } userId)
        {
            await next(context);
            return;
        }

        Activity.Current?.SetTag(UserIdTag, userId.Value);
        using (
            logger.BeginScope(new Dictionary<string, object?> { [UserIdProperty] = userId.Value })
        )
        {
            await next(context);
        }
    }
}
