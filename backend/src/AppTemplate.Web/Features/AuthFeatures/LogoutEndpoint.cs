using AppTemplate.UseCases.Authentication.Logout;
using AppTemplate.Web.Authentication;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

/// <summary>
/// Ends the sign-in session of the refresh token cookie and deletes the cookie. Access tokens
/// already issued stay valid until they expire (at most 15 minutes); the SPA drops its own.
/// </summary>
public class LogoutEndpoint(IMediator _mediator, AuthCookies _cookies)
    : EndpointWithoutRequest<Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/logout");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Sign out";
            s.Description =
                "Revokes the refresh token cookie's session and deletes the cookie. Requires an "
                + "allowed Origin and the X-XSRF-TOKEN header from GET /auth/antiforgery.";

            s.Responses[204] = "Signed out";
            s.Responses[403] = "Missing or invalid Origin or antiforgery token";
            s.Responses[409] = "Refreshed concurrently while signing out; sign out again";
        });

        Tags("Auth");

        Description(builder => builder.Produces(204).ProducesProblem(403).ProducesProblem(409));
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        if (await _cookies.RejectCrossSiteRequestAsync(HttpContext) is { } rejection)
        {
            return rejection;
        }

        var refreshToken = _cookies.ReadRefreshToken(HttpContext);
        _cookies.DeleteRefreshToken(HttpContext);
        if (refreshToken is null)
        {
            return TypedResults.NoContent();
        }

        var result = await _mediator.Send(new LogoutCommand(refreshToken), cancellationToken);
        return result.ToNoContentResult();
    }
}
