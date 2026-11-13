using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// The bearer tokens' settings (<c>Authentication:Jwt</c>, ADR 018), after NimblePros/eShopOnWeb's
/// <c>JwtSettings</c>. Validated at startup (<see cref="JwtSettingsValidator"/>). In Development
/// with no key set, each process makes up a random one (<see cref="DevelopmentJwtSigningKey"/>),
/// so there is no development key to leak into production; tokens then don't survive a restart.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Authentication:Jwt";

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>The HMAC key that signs and checks the tokens. A secret: keep it out of files.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "AppTemplate";

    public string Audience { get; set; } = "AppTemplate";

    /// <summary>
    /// How long a token is accepted. Short, because nothing revokes one before it expires: not a
    /// sign-out, a password change or a role change (ADR 018).
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 15;

    public TimeSpan TokenLifetime => TimeSpan.FromMinutes(TokenLifetimeMinutes);

    public SymmetricSecurityKey SecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
