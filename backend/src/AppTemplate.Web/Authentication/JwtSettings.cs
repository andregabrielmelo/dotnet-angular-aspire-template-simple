namespace AppTemplate.Web.Authentication;

/// <summary>How access tokens are signed and validated, and how long sessions last (<c>Jwt</c>).</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>The smallest HMAC-SHA256 key accepted, in bytes.</summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Base64 of a random key of at least 32 bytes (<c>openssl rand -base64 64</c>). A secret:
    /// in production it comes from the <c>jwt-signing-key</c> parameter, never appsettings.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>A refresh token unused for this long expires.</summary>
    public TimeSpan RefreshTokenSlidingLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>A sign-in session ends after this long, however often it refreshes.</summary>
    public TimeSpan RefreshTokenAbsoluteLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>A refresh token rotated this recently gets a 409, not reuse detection.</summary>
    public TimeSpan RefreshTokenReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(10);

    public byte[] SigningKeyBytes() => Convert.FromBase64String(SigningKey);

    public static bool IsValidSigningKey(string? signingKey)
    {
        var buffer = new byte[(signingKey?.Length ?? 0) * 3 / 4 + 3];
        return Convert.TryFromBase64String(signingKey ?? string.Empty, buffer, out var length)
            && length >= MinimumSigningKeyBytes;
    }
}
