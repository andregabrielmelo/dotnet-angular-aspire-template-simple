using AppTemplate.Web.Authentication;

namespace AppTemplate.Web.Features.AuthFeatures;

/// <summary>
/// Sets the antiforgery cookies. The SPA calls it before its first refresh or sign-out of a
/// page load (signing in sets them too); Angular then sends the token as X-XSRF-TOKEN.
/// </summary>
public class AntiforgeryEndpoint(AuthCookies _cookies) : EndpointWithoutRequest<NoContent>
{
    public override void Configure()
    {
        Get("/auth/antiforgery");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get antiforgery tokens";
            s.Description =
                "Sets the XSRF-TOKEN cookie, whose value /auth/refresh and /auth/logout expect in the X-XSRF-TOKEN header.";
            s.Responses[204] = "Cookies set";
        });

        Tags("Auth");

        Description(builder => builder.Produces(204));
    }

    public override Task<NoContent> ExecuteAsync(CancellationToken cancellationToken)
    {
        _cookies.IssueAntiforgery(HttpContext);
        return Task.FromResult(TypedResults.NoContent());
    }
}
