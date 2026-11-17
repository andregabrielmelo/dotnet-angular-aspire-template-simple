using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// In Development, when no <c>Authentication:Jwt:SigningKey</c> is set, signs with a random key
/// made when the process starts: tokens work without any setup, and no development key exists to
/// end up in production. Elsewhere it does nothing, and <see cref="JwtSettingsValidator"/>
/// requires the setting.
/// </summary>
public sealed class DevelopmentJwtSigningKey(IHostEnvironment environment)
    : IPostConfigureOptions<JwtSettings>
{
    private readonly IHostEnvironment _environment = environment;

    public void PostConfigure(string? name, JwtSettings options)
    {
        if (_environment.IsDevelopment() && string.IsNullOrEmpty(options.SigningKey))
        {
            options.SigningKey = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(JwtSettings.MinimumKeyBytes)
            );
        }
    }
}
