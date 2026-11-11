namespace AppTemplate.Web.Settings;

/// <summary>
/// Request limits per client and window (<c>RateLimiting</c> in appsettings). A signed-in caller
/// counts against their own user id, anyone else against their address; the auth endpoints
/// have a stricter limit of their own (RateLimitingConfigurations).
/// </summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Requests per window for a signed-in user.</summary>
    public int AuthenticatedPermitLimit { get; set; } = 600;

    /// <summary>Requests per window for an anonymous client address.</summary>
    public int AnonymousPermitLimit { get; set; } = 120;

    /// <summary>
    /// Attempts per window, per client address, at the auth endpoints (sign-in, registration,
    /// refresh and the account email endpoints): against password guessing and email abuse.
    /// </summary>
    public int AuthenticationPermitLimit { get; set; } = 5;
}
