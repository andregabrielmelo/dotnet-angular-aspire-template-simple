using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.GetCurrent;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UnitTests.UseCases.Caching;

/// <summary>
/// CachingBehavior caches whole query results, which HybridCache serializes with System.Text.Json
/// (for Redis, and for L1 too, since the types are mutable). They must come back unchanged.
/// </summary>
public class CachedResultRoundTrip
{
    private static readonly string[] Tags = [CacheTags.User(UserId.From(1))];

    /// <summary>
    /// Stores <paramref name="value"/> through one cache, then reads it back through another that
    /// shares only the L2, as a second API instance would: the value is deserialized.
    /// </summary>
    private static async Task<T> RoundTripAsync<T>(T value)
    {
        var redis = TestCaches.CreateSharedDistributedCache();
        var cancellationToken = TestContext.Current.CancellationToken;
        await TestCaches
            .Create(redis)
            .SetAsync("key", value, tags: Tags, cancellationToken: cancellationToken);

        return await TestCaches
            .Create(redis)
            .GetOrCreateAsync<T>(
                "key",
                _ => throw new InvalidOperationException("The value should have been cached."),
                cancellationToken: cancellationToken
            );
    }

    [Fact]
    public async Task KeepsAUserWithTheirPhoneNumber()
    {
        Result<UserDto> result = new UserDto(
            UserId.From(1),
            TestUsers.Name,
            new PhoneNumber("+44", "5551234", "7")
        );

        var cached = await RoundTripAsync(result);

        cached.Status.ShouldBe(ResultStatus.Ok);
        cached.Value.Id.ShouldBe(UserId.From(1));
        cached.Value.Name.ShouldBe(TestUsers.Name);
        cached.Value.PhoneNumber.ShouldBe(new PhoneNumber("+44", "5551234", "7"));
    }

    [Fact]
    public async Task KeepsTheCurrentUser()
    {
        Result<CurrentUserDto> result = new CurrentUserDto(
            UserId.From(1),
            TestUsers.Name,
            TestUsers.Email
        );

        var cached = await RoundTripAsync(result);

        cached.Status.ShouldBe(ResultStatus.Ok);
        cached.Value.ShouldBe(result.Value);
    }

    [Fact]
    public async Task KeepsAPageOfUsers()
    {
        Result<AppTemplate.UseCases.PagedResult<UserDto>> result =
            new AppTemplate.UseCases.PagedResult<UserDto>(
                [new UserDto(UserId.From(1), TestUsers.Name, PhoneNumber.Unknown)],
                Page: 1,
                PerPage: 10,
                TotalCount: 1,
                TotalPages: 1
            );

        var cached = await RoundTripAsync(result);

        cached.Status.ShouldBe(ResultStatus.Ok);
        cached.Value.Page.ShouldBe(1);
        cached.Value.TotalCount.ShouldBe(1);
        cached.Value.Items.Single().Id.ShouldBe(UserId.From(1));
        cached.Value.Items.Single().Name.ShouldBe(TestUsers.Name);
    }

    [Fact]
    public async Task RemovingAUsersTagKeepsOtherUsersEntries()
    {
        var cache = TestCaches.Create(TestCaches.CreateSharedDistributedCache());
        var cancellationToken = TestContext.Current.CancellationToken;
        await cache.SetAsync(
            "users:id:1",
            "one",
            tags: [CacheTags.User(UserId.From(1))],
            cancellationToken: cancellationToken
        );
        await cache.SetAsync(
            "users:id:2",
            "two",
            tags: [CacheTags.User(UserId.From(2))],
            cancellationToken: cancellationToken
        );

        await cache.RemoveByTagAsync(CacheTags.User(UserId.From(1)), cancellationToken);

        (await ReadAsync(cache, "users:id:1")).ShouldBeNull();
        (await ReadAsync(cache, "users:id:2")).ShouldBe("two");
    }

    private static ValueTask<string?> ReadAsync(HybridCache cache, string key) =>
        cache.GetOrCreateAsync<string?>(
            key,
            _ => ValueTask.FromResult<string?>(null),
            new HybridCacheEntryOptions
            {
                Flags =
                    HybridCacheEntryFlags.DisableLocalCacheWrite
                    | HybridCacheEntryFlags.DisableDistributedCacheWrite,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
}
