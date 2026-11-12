using AppTemplate.FunctionalTests.User;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Web.Endpoints.Users;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.FunctionalTests.Caching;

/// <summary>
/// The cached user reads (CachingBehavior over HybridCache, ADR 017) are served from the cache, and
/// never serve stale data after a write: each test reads once to fill the cache, writes, and reads
/// again. These run without Redis, so HybridCache
/// is in-memory only; <see cref="RedisOutage"/> covers Redis.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserCaching(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetByIdIsServedFromTheCacheUntilAWriteDropsIt()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{target.Id.Value}");

        // Behind the cache's back: no use case runs, so nothing is invalidated.
        using (var scope = factory.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
                .UserProfiles.Where(user => user.Id == target.Id)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(user => user.Name, UserName.From("Changed Directly")),
                    TestContext.Current.CancellationToken
                );
        }

        (
            await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{target.Id.Value}")
        ).Name.ShouldBe(target.Name.Value);
    }

    [Fact]
    public async Task GetByIdAfterAnUpdateReturnsTheNewName()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{target.Id.Value}");

        (
            await admin.Client.PutAsJsonAsync(
                $"/users/{target.Id.Value}",
                new { name = "Renamed By Admin" },
                TestContext.Current.CancellationToken
            )
        ).EnsureSuccessStatusCode();

        (
            await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{target.Id.Value}")
        ).Name.ShouldBe("Renamed By Admin");
    }

    [Fact]
    public async Task GetByIdAfterADeleteReturnsNotFound()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{target.Id.Value}");

        await admin.Client.DeleteAndEnsureNoContentAsync($"/users/{target.Id.Value}");

        await admin.Client.GetAndEnsureNotFoundAsync($"/users/{target.Id.Value}");
    }

    [Fact]
    public async Task MeAfterBeingRenamedByAnAdminReturnsTheNewName()
    {
        var (me, session) = await factory.CreateSignedInSessionAsync();
        await session.Client.GetAndDeserializeAsync<UserMe.MeBody>("/users/me");
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);

        (
            await admin.Client.PutAsJsonAsync(
                $"/users/{me.Id.Value}",
                new { name = "Renamed By Admin" },
                TestContext.Current.CancellationToken
            )
        ).EnsureSuccessStatusCode();

        (await session.Client.GetAndDeserializeAsync<UserMe.MeBody>("/users/me")).Name.ShouldBe(
            "Renamed By Admin"
        );
    }
}
