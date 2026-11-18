namespace AppTemplate.Infrastructure.Caching;

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
