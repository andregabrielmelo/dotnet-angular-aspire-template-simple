using AppTemplate.FunctionalTests.ApiEndpoints;
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
    public async Task GetByIdBeforeTheUserExistsIsNotCached()
    {
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        var first = await factory.CreateUserAsync();
        await admin.Client.GetAndEnsureNotFoundAsync($"/users/{first.Id.Value + 1}");

        // Nothing drops users:{second.Id} in between, so a cached 404 would still answer.
        var second = await factory.CreateUserAsync();

        second.Id.Value.ShouldBe(first.Id.Value + 1);
        (
            await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{second.Id.Value}")
        ).Name.ShouldBe(second.Name.Value);
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

    private const string ListPath = "/users?page=1&per_page=100";

    [Fact]
    public async Task ListIsServedFromTheCacheUntilAnUpdateDropsIt()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        // HybridCache reads the factory's clock, which stands still: an entry stored at the same
        // instant its tag was last removed (by CreateUserAsync) counts as removed. Real time moves.
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath);

        await RenameBehindTheCacheAsync(target.Id, "Changed Directly");
        var cached = await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath);

        (
            await admin.Client.PutAsJsonAsync(
                $"/users/{target.Id.Value}",
                new { name = "Renamed By Admin" },
                TestContext.Current.CancellationToken
            )
        ).EnsureSuccessStatusCode();
        var afterUpdate = await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath);

        cached.Items.Single(user => user.Id == target.Id.Value).Name.ShouldBe(target.Name.Value);
        afterUpdate
            .Items.Single(user => user.Id == target.Id.Value)
            .Name.ShouldBe("Renamed By Admin");
    }

    [Fact]
    public async Task ListAfterARegistrationCountsTheNewUser()
    {
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        var before = await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath);

        (
            await new TestSession(factory).RegisterAsync($"user-{Guid.NewGuid():N}@example.com")
        ).EnsureSuccessStatusCode();

        (
            await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath)
        ).TotalCount.ShouldBe(before.TotalCount + 1);
    }

    [Fact]
    public async Task ListAfterADeleteLeavesTheUserOut()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);
        (
            await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath)
        ).Items.ShouldContain(user => user.Id == target.Id.Value);

        await admin.Client.DeleteAndEnsureNoContentAsync($"/users/{target.Id.Value}");

        (
            await admin.Client.GetAndDeserializeAsync<UserList.UserPage>(ListPath)
        ).Items.ShouldNotContain(user => user.Id == target.Id.Value);
    }

    /// <summary>Renames a user in the database directly: no use case runs, so nothing is invalidated.</summary>
    private async Task RenameBehindTheCacheAsync(UserId id, string name)
    {
        using var scope = factory.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .UserProfiles.Where(user => user.Id == id)
            .ExecuteUpdateAsync(
                set => set.SetProperty(user => user.Name, UserName.From(name)),
                TestContext.Current.CancellationToken
            );
    }
}
