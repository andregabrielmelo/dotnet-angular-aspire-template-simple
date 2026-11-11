using AppTemplate.Web.Configurations;
using Microsoft.AspNetCore.Antiforgery;

namespace AppTemplate.Web.Endpoints.Authentication;

/// <summary>
/// Issues an antiforgery token for the caller, signed in or not (ADR 015): the HttpOnly half as
/// one cookie, and the half the SPA sends back as <c>X-XSRF-TOKEN</c> as the readable
/// <c>XSRF-TOKEN</c> cookie. Tokens are bound to the user, so fetch one again after signing in.
/// </summary>
public class AntiforgeryTokenEndpoint(IAntiforgery _antiforgery) : EndpointWithoutRequest<NoContent>
{
    public override void Configure()
    {
        Get("/auth/antiforgery-token");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get an antiforgery token";
            s.Description =
                "Sets the XSRF-TOKEN cookie. Signed-in requests other than GET must send its "
                + "value as the X-XSRF-TOKEN header. Fetch a new one after signing in.";
            s.Responses[204] = "The response sets the token cookies";
        });

        Tags("Auth");

        Description(builder => builder.Produces(204));
    }

    public override Task<NoContent> ExecuteAsync(CancellationToken cancellationToken)
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        HttpContext.Response.Cookies.Append(
            AntiforgeryConfigurations.RequestTokenCookieName,
            tokens.RequestToken!,
            AntiforgeryConfigurations.RequestTokenCookieOptions(HttpContext)
        );
        return Task.FromResult(TypedResults.NoContent());
    }
}
