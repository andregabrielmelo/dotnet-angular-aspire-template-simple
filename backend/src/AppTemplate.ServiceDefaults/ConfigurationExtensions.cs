using Microsoft.Extensions.Configuration;

namespace AppTemplate.ServiceDefaults;

public static class ConfigurationExtensions
{
    /// <summary>
    /// A configuration value the host can't run without. Missing or blank, it fails startup
    /// with the key in the message, instead of falling back to a literal at the call site.
    /// </summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"The configuration value '{key}' is required but is missing or empty."
            )
            : value;
    }
}
