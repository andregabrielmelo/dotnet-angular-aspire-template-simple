namespace AppTemplate.Web.Configurations;

/// <summary>How access tokens are signed and how long tokens last (<c>Jwt</c> in appsettings).</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>FastEndpoints signs with the key's ASCII bytes; HMAC-SHA256 needs at least 32.</summary>
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// A secret. Empty in appsettings.json, so a deployment without its own key fails at startup;
    /// set it per environment (<c>Jwt__SigningKey</c>), never in a committed file except the
    /// development-only value in appsettings.Development.json.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenValidity { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenValidity { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// The path the refresh token cookie is sent to, as the browser sees it: the SPA calls the API
    /// through <c>/api</c> (the dev server proxy, or the production host), so
    /// <c>/api/auth/refresh-token</c>, the refresh endpoint and logout.
    /// </summary>
    public string RefreshCookiePath { get; set; } = "/api/auth/refresh-token";
}
