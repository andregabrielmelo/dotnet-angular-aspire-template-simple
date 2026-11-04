using Microsoft.IdentityModel.JsonWebTokens;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// FastEndpoints' token revocation middleware (https://fast-endpoints.com/docs/security), run
/// before authentication: rejects an access token whose <c>jti</c> was signed out
/// (<see cref="RevokedAccessTokens"/>). It only reads the token's id; authentication then checks
/// the signature, so a forged token fails there.
/// </summary>
public sealed class RevokedTokenChecker(RequestDelegate next, RevokedAccessTokens revoked)
    : JwtRevocationMiddleware(next)
{
    private static readonly JsonWebTokenHandler Handler = new();

    protected override Task<bool> JwtTokenIsValidAsync(string jwtToken, CancellationToken ct)
    {
        if (!Handler.CanReadToken(jwtToken))
        {
            return Task.FromResult(true); // not a JWT; authentication rejects it
        }

        var jti = Handler.ReadJsonWebToken(jwtToken).Id;
        return Task.FromResult(string.IsNullOrEmpty(jti) || !revoked.IsRevoked(jti));
    }

    protected override Task SendTokenRevokedResponseAsync(HttpContext ctx, CancellationToken ct) =>
        TypedResults
            .Problem(
                detail: "The access token has been revoked. Sign in again.",
                statusCode: StatusCodes.Status401Unauthorized
            )
            .ExecuteAsync(ctx);
}
