using AppTemplate.UseCases.Authentication.Logout;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// Signs out: deletes the session's refresh token (from its cookie) and revokes the caller's access
/// token (<see cref="RevokedAccessTokens"/>), so neither works again. It sits on the refresh route
/// so the browser sends it the refresh cookie.
/// </summary>
public class LogoutEndpoint(
    IMediator _mediator,
    RefreshTokenCookie _cookie,
    RevokedAccessTokens _revokedAccessTokens
) : EndpointWithoutRequest<Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("/auth/refresh-token");

        Summary(s =>
        {
            s.Summary = "Sign out";
            s.Description =
                "Deletes the refresh token cookie's session and revokes the access token sent "
                + "with this request.";
            s.Responses[204] = "Signed out";
            s.Responses[401] = "Missing, invalid or already revoked access token";
        });

        Tags("Auth");

        Description(builder => builder.Produces(204).ProducesProblem(401));
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        await _mediator.Send(new LogoutCommand(_cookie.Read(HttpContext)), cancellationToken);
        _cookie.Delete(HttpContext);

        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (
            jti is not null
            && long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var exp)
        )
        {
            _revokedAccessTokens.Revoke(jti, DateTimeOffset.FromUnixTimeSeconds(exp));
        }

        return TypedResults.NoContent();
    }
}
