using System.Text;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Refuses bearer token settings that would make tokens forgeable or unusable, at startup
/// (<c>ValidateOnStart</c>), naming the setting. Outside Development the key must be set, since
/// <see cref="DevelopmentJwtSigningKey"/> only fills it in there.
/// </summary>
public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var failures = new List<string>();
        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtSettings.MinimumKeyBytes)
        {
            failures.Add(
                $"{JwtSettings.SectionName}:SigningKey must be set, with at least {JwtSettings.MinimumKeyBytes} bytes (a random value: openssl rand -base64 32)."
            );
        }
        if (
            string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience)
        )
        {
            failures.Add($"{JwtSettings.SectionName} needs an Issuer and an Audience.");
        }
        if (options.TokenLifetimeMinutes is < 1 or > 24 * 60)
        {
            failures.Add(
                $"{JwtSettings.SectionName}:TokenLifetimeMinutes must be between 1 and 1440."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
