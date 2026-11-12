using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppTemplate.UnitTests;

/// <summary>Real HybridCache instances, for tests where caching behavior is what's under test.</summary>
public static class TestCaches
{
    /// <summary>
    /// A HybridCache whose L2 is <paramref name="distributedCache"/>. Two instances sharing one
    /// distributed cache behave like two API instances sharing Redis: values read by the second
    /// really went through serialization.
    /// </summary>
    public static HybridCache Create(IDistributedCache distributedCache) =>
        new ServiceCollection()
            .AddSingleton(distributedCache)
            .AddHybridCache()
            .Services.BuildServiceProvider()
            .GetRequiredService<HybridCache>();

    /// <summary>An in-memory stand-in for Redis.</summary>
    public static IDistributedCache CreateSharedDistributedCache() =>
        new SharedDistributedCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
        );

    /// <summary>
    /// Stands in for Redis. HybridCache deliberately ignores a plain MemoryDistributedCache as
    /// its L2 (that would only duplicate L1), so it is wrapped in a different type.
    /// </summary>
    private sealed class SharedDistributedCache(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            inner.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            inner.Set(key, value, options);

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default
        ) => inner.SetAsync(key, value, options, token);

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            inner.RefreshAsync(key, token);

        public void Remove(string key) => inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            inner.RemoveAsync(key, token);
    }
}
