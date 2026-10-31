using AppTemplate.UseCases.Authentication.Refresh;
using AppTemplate.Web.Authentication;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

/// <summary>
/// Exchanges the refresh token cookie for a new access token and a new refresh token. The
/// cookie authenticates this request, so it must also come from an allowed origin with the
/// antiforgery header (see <see cref="AuthCookies"/>).
/// </summary>
public class RefreshEndpoint(IMediator _mediator, AuthCookies _cookies)
    : EndpointWithoutRequest<Results<Ok<AccessTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Refresh the session";
            s.Description =
                "Rotates the refresh token cookie and returns a new access token. Requires an "
                + "allowed Origin and the X-XSRF-TOKEN header from GET /auth/antiforgery.";
            s.ResponseExamples[200] = new AccessTokenResponse("eyJhbGciOiJIUzI1NiJ9...", 900);

            s.Responses[200] = "Session refreshed";
            s.Responses[401] = "No valid refresh token; sign in again";
            s.Responses[403] = "Missing or invalid Origin or antiforgery token";
            s.Responses[409] = "Refreshed concurrently by another request; retry";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Produces<AccessTokenResponse>(200, "application/json")
                .ProducesProblem(401)
                .ProducesProblem(403)
                .ProducesProblem(409)
        );
    }

    public override async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        if (await _cookies.RejectCrossSiteRequestAsync(HttpContext) is { } rejection)
        {
            return rejection;
        }

        var refreshToken = _cookies.ReadRefreshToken(HttpContext);
        if (refreshToken is null)
        {
            return Result.Unauthorized().ToProblem();
        }

        var result = await _mediator.Send(
            new RefreshSessionCommand(refreshToken),
            cancellationToken
        );
        if (result.Status == ResultStatus.Unauthorized)
        {
            _cookies.DeleteRefreshToken(HttpContext);
        }

        return result.ToOkResult(session => _cookies.SignIn(HttpContext, session));
    }
}
