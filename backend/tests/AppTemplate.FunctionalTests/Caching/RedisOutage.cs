using System.Diagnostics;
using AppTemplate.FunctionalTests.ApiEndpoints;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Web.Endpoints.Users;
using Microsoft.EntityFrameworkCore;
using Testcontainers.Redis;

namespace AppTemplate.FunctionalTests.Caching;

/// <summary>
/// Redis is optional for reads (ADR 017): with it stopped, they keep answering at once. HybridCache
/// logs and skips a failing L2, falling back to its in-memory layer and Postgres, and the Redis
/// client fails each command fast while disconnected (BacklogPolicy.FailFast) instead of waiting
/// out its 5-second timeout. A write still saves, but answers 500, because invalidating its cached
/// reads (RemoveByTagAsync) needs Redis.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RedisOutage(RedisOutage.RedisFactory factory) : IClassFixture<RedisOutage.RedisFactory>
{
    [Fact]
    public async Task WithRedisStoppedReadsStillAnswerQuicklyAndWritesSaveButFail()
    {
        var (me, session) = await factory.CreateSignedInSessionAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);

        // Fill both layers while Redis runs.
        await session.Client.GetAndDeserializeAsync<UserMe.MeBody>("/users/me");
        await admin.Client.GetAndDeserializeAsync<UserRecord>($"/users/{me.Id.Value}");

        await factory.Redis.StopAsync(TestContext.Current.CancellationToken);
        var stopwatch = Stopwatch.StartNew();

        var byId = await admin.Client.GetAsync(
            $"/users/{me.Id.Value}",
            TestContext.Current.CancellationToken
        );
        var myself = await session.Client.GetAsync(
            "/users/me",
            TestContext.Current.CancellationToken
        );
        var update = await admin.Client.PutAsJsonAsync(
            $"/users/{me.Id.Value}",
            new { name = "Renamed Without Redis" },
            TestContext.Current.CancellationToken
        );
        stopwatch.Stop();

        byId.StatusCode.ShouldBe(HttpStatusCode.OK);
        myself.StatusCode.ShouldBe(HttpStatusCode.OK);
        // The change is saved, but invalidating its cached reads needs Redis (ADR 017).
        update.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await SavedNameAsync(me.Id)).ShouldBe("Renamed Without Redis");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    private async Task<string> SavedNameAsync(UserId id)
    {
        using var scope = factory.Services.CreateScope();
        var user = await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .UserProfiles.AsNoTracking()
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken);
        return user.Name.Value;
    }

    /// <summary>The usual factory, with a real Redis as HybridCache's L2.</summary>
    public sealed class RedisFactory : CustomWebApplicationFactory<Program>
    {
        public RedisContainer Redis { get; } = new RedisBuilder("redis:8.2").Build();

        public override async ValueTask InitializeAsync()
        {
            await Redis.StartAsync();
            await base.InitializeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(
                $"ConnectionStrings:{InfrastructureServiceExtensions.RedisConnectionName}",
                Redis.GetConnectionString()
            );
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await Redis.DisposeAsync();
        }
    }
}
