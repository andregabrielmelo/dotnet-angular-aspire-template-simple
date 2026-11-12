using Ardalis.SmartEnum;

namespace AppTemplate.UseCases.Caching;

/// <summary>
/// How long a cached query lives (<see cref="ICacheable.CacheProfile"/>; ADR 017). The profiles
/// are named here, so a query can't name one that doesn't exist; their lifetimes are settings:
/// <see cref="None"/> uses <c>Caching:Expiration</c> and <c>Caching:LocalExpiration</c>, and every
/// other profile <c>Caching:Profiles:{Name}</c>, all validated at startup.
/// </summary>
public sealed class CacheProfile : SmartEnum<CacheProfile>
{
    /// <summary>No specific profile: the default lifetimes (10 minutes, 1 in memory).</summary>
    public static readonly CacheProfile None = new(nameof(None), 0);

    /// <summary>For data that goes stale quickly (1 minute, 15 seconds in memory).</summary>
    public static readonly CacheProfile Short = new(nameof(Short), 1);

    /// <summary>For data that rarely changes (1 hour, 5 minutes in memory).</summary>
    public static readonly CacheProfile Long = new(nameof(Long), 2);

    private CacheProfile(string name, int value)
        : base(name, value) { }
}
