namespace AppTemplate.FunctionalTests.Security;

/// <summary>The real host with low limits, so a test can reach them in a few requests.</summary>
public sealed class LowRateLimitsFactory : CustomWebApplicationFactory<Program>
{
    public const int AuthenticationPermitLimit = 3;
    public const int AuthenticatedPermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(
            "RateLimiting:AuthenticationPermitLimit",
            AuthenticationPermitLimit.ToString()
        );
        builder.UseSetting(
            "RateLimiting:AuthenticatedPermitLimit",
            AuthenticatedPermitLimit.ToString()
        );
    }
}

/// <summary>The <c>authentication</c> policy: a few attempts per client address and window.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RateLimitingAuthenticationPolicy(LowRateLimitsFactory factory)
    : IClassFixture<LowRateLimitsFactory>
{
    [Fact]
    public async Task AnswersTooManyRequestsWithRetryAfterOnceTheLimitIsReached()
    {
        // A client can't escape its partition by claiming another address: the test server's
        // peer isn't a known proxy, so X-Forwarded-For is ignored.
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt <= LowRateLimitsFactory.AuthenticationPermitLimit; attempt++)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"203.0.113.{attempt}");
            last = await client.PostAsJsonAsync(
                "/auth/login",
                new { email = "nobody@example.com", password = "wrong password" },
                TestContext.Current.CancellationToken
            );
        }

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        last.Headers.RetryAfter.ShouldNotBeNull();
        last.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}

/// <summary>The global limiter: each signed-in user has a partition of their own.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RateLimitingPerUser(LowRateLimitsFactory factory) : IClassFixture<LowRateLimitsFactory>
{
    private async Task<TestSession> SignInAsync()
    {
        var user = await factory.CreateUserAsync();
        var session = new TestSession(factory);
        (
            await session.LoginAsync(user.Email.Value, fetchAntiforgeryToken: false)
        ).EnsureSuccessStatusCode();
        return session;
    }

    [Fact]
    public async Task LimitsOneUserWithoutAffectingAnother()
    {
        // Signed in without fetching an antiforgery token, which would count as a request.
        var ada = await SignInAsync();
        var grace = await SignInAsync();

        for (var request = 0; request < LowRateLimitsFactory.AuthenticatedPermitLimit; request++)
        {
            (
                await ada.Client.GetAsync("/users/me", TestContext.Current.CancellationToken)
            ).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (
            await ada.Client.GetAsync("/users/me", TestContext.Current.CancellationToken)
        ).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (
            await grace.Client.GetAsync("/users/me", TestContext.Current.CancellationToken)
        ).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
