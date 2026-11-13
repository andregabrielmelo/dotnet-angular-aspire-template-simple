using System.Security.Claims;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Interfaces;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Signs HS256 JWTs (ADR 018), after NimblePros/eShopOnWeb's <c>IdentityTokenClaimService</c>.
/// The claims come from the session's own factory (<see cref="ApplicationClaimsPrincipalFactory"/>),
/// so a token authorizes exactly what the cookie does, minus the security stamp, which nothing
/// checks in a token and which a readable token shouldn't reveal. Times come from
/// <see cref="TimeProvider"/>, which <see cref="TokenAuthentication"/> also checks them with.
/// </summary>
public sealed class IdentityTokenClaimService(
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> identityOptions,
    IOptions<JwtSettings> jwtSettings,
    TimeProvider time
) : ITokenClaimsService
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public async Task<AccessToken> GetTokenAsync(UserId userId, CancellationToken cancellationToken)
    {
        var account = Guard.Against.NotFound(
            userId,
            await userManager.Users.SingleOrDefaultAsync(
                account => account.ProfileId == userId,
                cancellationToken
            )
        );
        var principal = await claimsFactory.CreateAsync(account);
        var stampClaimType = identityOptions.Value.ClaimsIdentity.SecurityStampClaimType;

        var settings = jwtSettings.Value;
        var now = time.GetUtcNow();
        var expiresAt = now + settings.TokenLifetime;
        var token = TokenHandler.CreateToken(
            new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                    principal.Claims.Where(claim => claim.Type != stampClaimType)
                ),
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    settings.SecurityKey(),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );
        return new AccessToken(token, expiresAt);
    }
}
