using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Infrastructure.Caching;

/// <summary>
/// How long HybridCache keeps entries (<c>Caching</c> in settings; ADR 017), per
/// <see cref="CacheProfile"/>. Validated at startup (OptionConfigurations): every profile has an
/// entry, and none keeps a copy in memory longer than in Redis.
/// </summary>
public sealed class CachingSettings
{
    public const string SectionName = "Caching";

    /// <summary>The whole lifetime of an entry, in Redis (L2), for <see cref="CacheProfile.None"/>.</summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The lifetime in each instance's memory (L1), for <see cref="CacheProfile.None"/>. Short,
    /// because it bounds how long another instance can serve a copy after an invalidation. At most
    /// <see cref="Expiration"/>.
    /// </summary>
    public TimeSpan LocalExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>The other profiles' lifetimes, by <see cref="CacheProfile"/> name.</summary>
    public Dictionary<string, CacheLifetime> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [CacheProfile.Short.Name] = new()
            {
                Expiration = TimeSpan.FromMinutes(1),
                LocalExpiration = TimeSpan.FromSeconds(15),
            },
            [CacheProfile.Long.Name] = new()
            {
                Expiration = TimeSpan.FromHours(1),
                LocalExpiration = TimeSpan.FromMinutes(5),
            },
        };

    /// <summary>The HybridCache lifetimes of <paramref name="profile"/>.</summary>
    public HybridCacheEntryOptions EntryOptionsFor(CacheProfile profile)
    {
        var lifetime =
            profile == CacheProfile.None
                ? new CacheLifetime { Expiration = Expiration, LocalExpiration = LocalExpiration }
                : Profiles[profile.Name];

        return new HybridCacheEntryOptions
        {
            Expiration = lifetime.Expiration,
            LocalCacheExpiration = lifetime.LocalExpiration,
        };
    }
}

/// <summary>One cache profile's lifetimes (<see cref="CachingSettings.Profiles"/>).</summary>
public sealed class CacheLifetime
{
    /// <summary>The whole lifetime of an entry, in Redis (L2).</summary>
    public TimeSpan Expiration { get; set; }

    /// <summary>The lifetime in each instance's memory (L1); at most <see cref="Expiration"/>.</summary>
    public TimeSpan LocalExpiration { get; set; }

    /// <summary>Both positive, and the memory copy never outlives the Redis one.</summary>
    public bool IsValid =>
        Expiration > TimeSpan.Zero
        && LocalExpiration > TimeSpan.Zero
        && LocalExpiration <= Expiration;
}
