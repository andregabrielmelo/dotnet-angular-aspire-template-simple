using System.Security.Claims;
using System.Security.Cryptography;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authentication;
using AppTemplate.Web.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// Signs access tokens (HS256 JWTs) carrying who the user is and what they may do, and creates
/// refresh token values. The claims use JWT names; validation keeps them (MapInboundClaims).
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtSettings> settings, TimeProvider timeProvider)
    : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken IssueAccessToken(User user, IReadOnlyCollection<string> permissions)
    {
        var jwt = settings.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + jwt.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Name, user.Name.Value),
        };
        claims.AddRange(
            permissions.Select(permission => new Claim(AppClaimTypes.Permission, permission))
        );

        var token = _handler.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = jwt.Issuer,
                Audience = jwt.Audience,
                Subject = new ClaimsIdentity(claims),
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(jwt.SigningKeyBytes()),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );

        return new AccessToken(token, expiresAt);
    }

    public string CreateRefreshTokenValue() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
}
