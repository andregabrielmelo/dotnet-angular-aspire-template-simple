using System.Diagnostics;
using AppTemplate.Web.Middleware;
using Microsoft.Extensions.Logging.Testing;

namespace AppTemplate.FunctionalTests.Observability;

/// <summary>
/// <see cref="UserContextEnrichmentMiddleware"/>: a signed-in request's trace carries the user's
/// id, and so does every log written while handling it.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserContextEnrichment(UserContextEnrichment.Factory factory)
    : IClassFixture<UserContextEnrichment.Factory>
{
    /// <summary>The usual factory, with every log also collected for the assertions.</summary>
    public sealed class Factory : CustomWebApplicationFactory<Program>
    {
        public FakeLogCollector Logs => Services.GetRequiredService<FakeLogCollector>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddFakeLogging());
        }
    }

    [Fact]
    public async Task TagsTheRequestTraceWithTheUserId()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var requests = await RecordRequestActivitiesAsync(() =>
            session.Client.GetAsync("/users/me", TestContext.Current.CancellationToken)
        );

        requests
            .Single(activity => activity.GetTagItem("url.path") is "/users/me")
            .GetTagItem(UserContextEnrichmentMiddleware.UserIdTag)
            .ShouldBe(user.Id.Value);
    }

    [Fact]
    public async Task ScopesTheRequestLogsWithTheUserId()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();
        factory.Logs.Clear();

        (
            await session.Client.GetAsync("/users/me", TestContext.Current.CancellationToken)
        ).EnsureSuccessStatusCode();

        // LoggingBehavior logs the query by name while the request is handled.
        var handling = factory
            .Logs.GetSnapshot()
            .First(record => record.Message == "Handling GetCurrentUserQuery");
        ScopeValues(handling)
            .ShouldContain(
                new KeyValuePair<string, object?>(
                    UserContextEnrichmentMiddleware.UserIdProperty,
                    user.Id.Value
                )
            );
    }

    [Fact]
    public async Task LeavesAnonymousRequestsUntagged()
    {
        factory.Logs.Clear();

        var requests = await RecordRequestActivitiesAsync(() =>
            new TestSession(factory).Client.GetAsync(
                "/auth/antiforgery-token",
                TestContext.Current.CancellationToken
            )
        );

        requests
            .Single(activity => activity.GetTagItem("url.path") is "/auth/antiforgery-token")
            .GetTagItem(UserContextEnrichmentMiddleware.UserIdTag)
            .ShouldBeNull();
        factory
            .Logs.GetSnapshot()
            .SelectMany(ScopeValues)
            .ShouldNotContain(pair => pair.Key == UserContextEnrichmentMiddleware.UserIdProperty);
    }

    /// <summary>The ASP.NET Core request activities that finish while <paramref name="send"/> runs.</summary>
    private static async Task<List<Activity>> RecordRequestActivitiesAsync(
        Func<Task<HttpResponseMessage>> send
    )
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (stopped)
                {
                    stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (await send()).EnsureSuccessStatusCode();

        lock (stopped)
        {
            return [.. stopped];
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> ScopeValues(FakeLogRecord record) =>
        record.Scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>().SelectMany(s => s);
}
